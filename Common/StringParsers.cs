using System;
using System.Text.RegularExpressions;

public class StringParsers
{
    public static string ParseResourceGroupName(string resourceId)
    {
        Regex rx = new Regex(@"\/resourceGroups\/(.*)", RegexOptions.IgnoreCase);
        MatchCollection matches = rx.Matches(resourceId);
        return matches[0].Groups[1].Value;
    }

    public static (string, string, string) ParseCommand(string command)
    {
        Regex rx = new Regex(@"(\w*?) (\d*?) (\d*)$", RegexOptions.IgnoreCase);
        MatchCollection matches = rx.Matches(command);
        if(matches.Count < 1)
            throw new ArgumentException("Found no matches for parsing command.");
        
        // some commands only have 1 argument
        if(matches[0].Groups.Count == 2)
            return (matches[0].Groups[1].Value.ToLower(), matches[0].Groups[2].Value, null);

        return (matches[0].Groups[1].Value.ToLower(), matches[0].Groups[2].Value, matches[0].Groups[3].Value);
    }

    public static string ParseTextMessage(string requestBody)
    {
        Regex rx = new Regex(@".*?\&Body=(.*?)\&FromCountry", RegexOptions.IgnoreCase);
        MatchCollection matches = rx.Matches(requestBody);
        return matches[0].Groups[1].Value;
    }
}