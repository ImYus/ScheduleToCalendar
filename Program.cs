using System;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using System.Globalization;
using System.Diagnostics.Metrics;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading;


class WebMethods
{
    public async Task<string> GetPage(string adress)
    {
        try
        {
            using (WebClient client = new WebClient())
            {
                string request = await client.DownloadStringTaskAsync(adress);
                return request;
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

class Program
{
    static string FindGroup(string content, string group)
    {
        string findWord = $"\"{group}\":[`<tr>";
        int startIndex = content.IndexOf(findWord);
        string smaller = content.Substring(startIndex);
        HtmlDocument document = new HtmlDocument();
        document.LoadHtml(smaller);
        int trCount = 0;
        HtmlNodeCollection trNodes = document.DocumentNode.SelectNodes("//tr");

        if (trNodes != null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (HtmlNode node in trNodes)
            {
                if (trCount < 7)
                {
                    sb.AppendLine(node.OuterHtml);
                    trCount++;
                }
                else
                {
                    break;
                }
            }
            string extractedHtml = sb.ToString();
            return extractedHtml;
        }
        return "-1";
    }
    static List<string> ListDates(string content)
    {
        HtmlDocument document = new HtmlDocument();
        document.LoadHtml(content);
        HtmlNode firstTr = document.DocumentNode.SelectSingleNode("//tr");
        List<string> tdContents = new List<string>();
        List<string> numsOnly = new List<string>();
        if (firstTr != null)
        {

            HtmlNodeCollection tdNodes = firstTr.SelectNodes("td");
            if (tdNodes != null)
            {
                foreach (HtmlNode node in tdNodes)
                {
                    tdContents.Add(node.InnerHtml.Trim());
                }

                tdContents.RemoveRange(0, 2);
                foreach (string i in tdContents)
                {
                    string[] parts = i.Split(',');
                    if (parts.Length == 2)
                    {
                        string date = parts[1].Trim();
                        numsOnly.Add(date);
                    }
                }
                return numsOnly;

            }
        }
        return numsOnly;
    }
    static (string[], string[]) GetTime(string content)
    {
        string[] dirtyArr = new string[5];

        string[] start = new string[5];
        string[] end = new string[5];
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(content);
        var trNodes = doc.DocumentNode.SelectNodes("//tr");
        int i = 0;
        foreach (HtmlNode node in trNodes)
        {
            int j = 0;
            var tdNodes = node.SelectNodes("td");
            foreach (HtmlNode tdNode in tdNodes)
            {
                if (i >= 2 && j == 1)
                {
                    dirtyArr[i - 2] = tdNode.OuterHtml;
                }
                j++;
            }
            i++;
        }
        for (int c = 0; c < dirtyArr.Length; c++)
        {
            doc.LoadHtml(dirtyArr[c]);
            HtmlNode tdElement = doc.DocumentNode.SelectSingleNode("//td");
            string tdContent = tdElement.InnerHtml;
            string[] arr = tdContent.Split("<hr class=\"hrMin\">");
            for(int r  = 0; r < arr.Length; r++)
            {
                if(r%2 == 0)
                {
                    start[c] = arr[r];
                }
                else
                {
                    end[c] = arr[r];
                }
            }
        }
        return (start, end);
    }
    static (string[], string[]) FormatArr(string[] start, string[] end)
    {
        string[] fStart = new string[start.Length];
        string[] fEnd = new string[end.Length];
        for(int i  = 0; i < start.Length; i++)
        {
            string[] parts = start[i].Split('.');
            string hours = parts[0];
            string minutes = parts[1];
            string result = $"{int.Parse(hours)}:{minutes}:00";
            fStart[i] = result;
            string[] parts1 = end[i].Split(".");
            string hours1 = parts1[0];
            string minutes1 = parts1[1];
            string result1 = $"{int.Parse(hours1)}:{minutes1}:00";
            fEnd[i] = result1;
        }
        return (fStart, fEnd);
    }
    static string DeleteTrash(string content)
    {
        HtmlDocument document = new HtmlDocument();
        document.LoadHtml(content);
        var trNodes = document.DocumentNode.SelectNodes("//tr");
        if (trNodes.Count >= 2)
        {
            trNodes[0].Remove();
            trNodes[1].Remove();
        }

        foreach (var i in trNodes)
        {
            var j = i.SelectNodes("td");
            if (j.Count >= 2)
            {
                j[0].Remove();
                j[1].Remove();
            }
        }
        return document.DocumentNode.OuterHtml;
    }
    static async Task CreateEvent(string className, DateTime startTime, DateTime endTime)
    {
        string jsonKeyFilePath = "conffile.json";
        string calendarIdPath = "calendar_id.txt";
        string calendarId = "";
        using (StreamReader reader = new StreamReader(calendarIdPath))
        {
            calendarId = reader.ReadToEnd();
        }

        var credential = GoogleCredential.FromFile(jsonKeyFilePath).CreateScoped(CalendarService.Scope.Calendar);

        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ScheduleToCalendar",
        });
        var newEvent = new Event
        {
            Summary = className,
            Start = new EventDateTime { DateTime = startTime },
            End = new EventDateTime { DateTime = endTime },
        };

        var request = service.Events.Insert(newEvent, calendarId);
        Event createdEvent = request.Execute();
        Console.WriteLine("Event created " + createdEvent.HtmlLink);
    }
    static List<string> ConvertDates(List<string> dates)
    {
        List<string> convertedDates = new List<string>();
        foreach (string date in dates)
        {
            string[] parts = date.Split('.');
            string convertedDate = parts[2] + "-" + parts[1] + "-" + parts[0];
            convertedDates.Add(convertedDate);
        }
        return convertedDates;
    } 

    static async Task Filler(string str, List<string> dates, string[] startArr, string[] endArr)
    {
        Console.WriteLine(str);
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(str);

        var trNodes = doc.DocumentNode.SelectNodes(".//tr");
        int count = 0;
        if (trNodes != null)
        {
            foreach (var tr in trNodes)
            {
                var tdNodes = tr.SelectNodes(".//td");
                if (tdNodes != null)
                {
                    for (int i = 0; i < tdNodes.Count; i++)
                    {
                        var tdNode = tdNodes[i];
                        var tdContent = tdNodes[i].InnerText.Trim();
                        if (!string.IsNullOrEmpty(tdContent))
                        {
                            Console.WriteLine(tdContent);
                            await CreateEvent(tdContent, DateTime.Parse($"{dates[i]} {startArr[count]}"), DateTime.Parse($"{dates[i]} {endArr[count]}"));

                        }
                    }
                }
                count++;
            }
        }
    }
    static async Task Main()
    {
        WebMethods wm = new WebMethods();

        string resp = FindGroup(await wm.GetPage("https://ppk.sstu.ru/schedule/"), "ИСП-924");
        List<string> arrstr = ListDates(resp);
        List<string> newList = ConvertDates(arrstr);
        (string[] start, string[] end) = GetTime(resp);
        (string[] start1, string[] end1) = FormatArr(start, end);
        await Filler(DeleteTrash(resp), newList, start1, end1);
    }
} 