using System.Text.RegularExpressions;

public class StringParsers
{
    public static string ParseResourceGroupName(string resourceId)
    {
        Regex rx = new Regex(@"\/resourceGroups\/(.*)", RegexOptions.IgnoreCase);
        MatchCollection matches = rx.Matches(resourceId);
        return matches[0].Groups[1].Value;
    }
}