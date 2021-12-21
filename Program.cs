using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ReleaseNotesReader
{
    static class Extensions
    {
        public static void Print<T>(this IEnumerable<T> sequence, Action<T> printAction)
        {
            foreach (T item in sequence)
            {
                printAction(item);
            }
        }
    }

    class Ticket
    {
        public string Title { get; set; }

        public string Type { get; set; }

        public string Access { get; set; }

        public string Number { get; set; }

        public string Description { get; set; }

        public override string ToString() => $"{Title}\n{Type}\n{Access}\n{Number}\n{Description}\n";
    }

    class Program
    {
        const int HyphenWithPeriods = 3;

        const int GenericTicketTitleBeginningSymbols = 11;

        const string GenericTicketTitleEnding = "EveryMatrix JIRA";

        const string GenericTicketLinkEnding = "?src=confmacro";

        private static byte[] AuthenticationBytes => Encoding.ASCII.GetBytes("olesia.falafivka@everymatrix.com:xGXgyuJchkFWnzHfoPzf4D97");

        private static async Task<HttpResponseMessage> GetFromLink(string link)
        {
            using HttpClient confClient = new();
            confClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(AuthenticationBytes));
            return await confClient.GetAsync(link);
        }

        private static async Task Main()// how to implement using classes?
        {
            Console.WriteLine("Enter link:");
            string userINput = Console.ReadLine();

            HttpResponseMessage message = await GetFromLink(userINput);

            if (!message.IsSuccessStatusCode)
            {
                Console.WriteLine("Unable to reach the website");
                return;
            }

            string response = await message.Content.ReadAsStringAsync();
            //await File.WriteAllTextAsync("releaseNotes.txt", response);
            var htmlParser = new HtmlParser();
            var document = await htmlParser.ParseDocumentAsync(response); // or read from file

            List<string> stringsList = new();

            foreach (var element in document.QuerySelectorAll("tr > td"))
            {
                string textElement = element.TextContent.Trim();
                if (!string.IsNullOrEmpty(textElement))
                {
                    stringsList.Add(textElement);
                }
            }

            List<Ticket> ticketList = new();

            for (int i = 0; i < stringsList.Count; i++)
            {
                if (int.TryParse(stringsList[i], out _))
                {
                    string jiraNumber = stringsList[i + 1];
                    if (jiraNumber.Contains(GenericTicketLinkEnding))
                    {
                        jiraNumber = jiraNumber[..jiraNumber.IndexOf(GenericTicketLinkEnding)];
                    }

                    string jiraTitle = string.Empty;

                    HttpResponseMessage ticketPageMessage = await GetFromLink(jiraNumber);

                    if (!ticketPageMessage.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Cannot access the ticket page.");
                        return;
                    }

                    string ticketPageResponse = await ticketPageMessage.Content.ReadAsStringAsync();
                    var ticketDocument = await htmlParser.ParseDocumentAsync(ticketPageResponse);

                    string title = ticketDocument.QuerySelector("title").TextContent;
                    jiraTitle = title[GenericTicketTitleBeginningSymbols..(title.IndexOf(GenericTicketTitleEnding) - HyphenWithPeriods)].Trim();

                    Ticket ticket = new() // how to name dynamically?
                    {
                        Number = jiraNumber,
                        Title = jiraTitle,
                        Type = stringsList[i + 4],
                        Description = stringsList[i + 5],
                        Access = stringsList[i + 6]
                    };

                    ticketList.Add(ticket);
                }
            }

            IOrderedEnumerable<Ticket> sortedTicketList = ticketList
                .OrderBy(o => o.Type)
                .ThenBy(t => t.Number);

            //a faster way: objectsList.Sort((x, y) => x.Type.CompareTo(y.Type));

            StringBuilder builder = new();

            foreach (var ticket in sortedTicketList)
            {
                Console.WriteLine($"{ticket}\n");
                builder.Append($"{ticket}\n");
            }

            await File.WriteAllTextAsync("release_notes.txt", $"{builder}"); //no return here?
        }
    }
}