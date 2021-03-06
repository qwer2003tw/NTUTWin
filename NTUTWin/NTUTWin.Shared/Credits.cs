﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NTUTWin
{
	class Credits
    {
        public class Credit
        {
            public string CourseId { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
            public float Credits { get; set; }
            public float Grade { get; set; }
            public string Note { get; set; }
            public CourseDetail Detail { get; set; }
        }

        public class Semester
        {
            public string Name { get; set; }
            public float TotalAverage { get; set; }
            public float ConductGrade { get; set; }
            public float CreditsWanted { get; set; }
            public float CreditsGot { get; set; }
            public List<Credit> Credits { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public List<Semester> Semesters { get; set; }

        public float TotalCreditsGot { get; private set; }

        public Dictionary<string, float> TotalTypeCredits { get; private set; } = new Dictionary<string, float>();

        public Dictionary<string, float> TotalDetailTypeCredits { get; private set; } = new Dictionary<string, float>();

        public static async Task<Credits> Parse(string html)
        {
            var blockMatch = new Regex("請先完成[^教]+教學評量。", RegexOptions.IgnoreCase | RegexOptions.Multiline).Match(html);
            if (blockMatch.Success)
                throw new System.Exception(blockMatch.Value);

            var credits = new Credits();
            var tableRegex = new Regex("<img src=\\./image/or_ball\\.gif>([^<]+)</h3>\n<table border=1 BGCOLOR=#CCFFFF>(?:(?!<\\/table>).|\n)*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var tablematches = tableRegex.Matches(html);
            var semesters = new List<Semester>();
            foreach (Match match in tablematches)
                semesters.Add(ParseTable(match));
            credits.Semesters = semesters;


			var tasks = new List<Task>();
			foreach (Semester semester in semesters)
			{
				tasks.AddRange(semester.Credits.Select(async credit =>
				{
					var courseDetail = await NPAPI.GetCourseDetail(credit.CourseId);

					//Set course detail
					credit.Detail = courseDetail;

					if (credit.Grade >= 60)
					{
						//Sum credits for each detailed type
						if (credits.TotalDetailTypeCredits.ContainsKey(credit.Detail.Type))
							credits.TotalDetailTypeCredits[credit.Detail.Type] += credit.Credits;
						else
							credits.TotalDetailTypeCredits.Add(credit.Detail.Type, credit.Credits);

						//Sum credits for each type
						if (credits.TotalTypeCredits.ContainsKey(credit.Type))
							credits.TotalTypeCredits[credit.Type] += credit.Credits;
						else
							credits.TotalTypeCredits.Add(credit.Type, credit.Credits);

						//Sum semester credits
						credits.TotalCreditsGot += credit.Credits;
					}
				}));
			}

			//Run all tasks in parallel
			await Task.WhenAll(tasks.ToArray());

            return credits;
        }

        public static Semester ParseTable(Match match)
        {
            Semester semester = new Semester();
            semester.Name = match.Groups[1].Value;
            var html = match.Value;

            //Parse Summary
            var summaryRegex = new Regex("<th colspan=2 BGCOLOR=99FF99>[^<]+<td colspan=6 align=center>\n([^\n]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var summaryMatches = summaryRegex.Matches(html);
            semester.TotalAverage = TryGetFloat(summaryMatches[0].Groups[1].Value, 0);
            semester.ConductGrade = TryGetFloat(summaryMatches[1].Groups[1].Value, 0);
            semester.CreditsWanted = TryGetFloat(summaryMatches[2].Groups[1].Value, 0);
            semester.CreditsGot = TryGetFloat(summaryMatches[3].Groups[1].Value, 0);

            //Parse Credits
            var creditRegex = new Regex(
                "<tr><th align=Right>([^\\s]*)\\s+" +
                "<th>([^\\s]*)\\s+" + 
                "<th align=left><a href=\"[^\"]+\">([^<]+)</a>\\s+" +
                "<th align=center>([^\\s]+)\\s+" +
                "<th align=center>([^\\s]+)\\s+" +
                "<th align=right>\\s*([^\\s]+)\\s+" +
                "<th align=right>\\s*([^\\s]+)\\s+" +
                "<td>([^\n]+)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var creditMatches = creditRegex.Matches(html);
            var credits = new List<Credit>();
            foreach (Match creditMatch in creditMatches)
            {
                Credit credit = new Credit();
                credit.CourseId = creditMatch.Groups[1].Value;
                credit.Type = creditMatch.Groups[2].Value;
                credit.Name = creditMatch.Groups[3].Value;
                credit.Credits = TryGetFloat(creditMatch.Groups[6].Value, 0);
                credit.Grade = TryGetFloat(creditMatch.Groups[7].Value, 0f);
                credit.Note = creditMatch.Groups[8].Value;
                credit.Note = new Regex("<[^>]+>").Replace(credit.Note, " ").Trim();
                credits.Add(credit);
            }
            semester.Credits = credits;
            return semester;
        }

        private static float TryGetFloat(string input, float defaultValue)
        {
            float output;
            if (float.TryParse(input, out output))
                return output;
            else
                return defaultValue;
        }
    }
}