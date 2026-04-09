using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SpeedyNtoNAssociatePlugin.Services
{
    internal class SyntaxHighlighter
    {
        // XML patterns
        private static readonly Regex XmlPunctuationRegex = new Regex(@"[<>/=]", RegexOptions.Compiled);
        private static readonly Regex XmlTagRegex = new Regex(@"(?<=</?)\w[\w\-]*", RegexOptions.Compiled);
        private static readonly Regex XmlAttributeRegex = new Regex(@"\b(\w[\w\-]*)(?=\s*=)", RegexOptions.Compiled);
        private static readonly Regex XmlValueRegex = new Regex("\"[^\"]*\"", RegexOptions.Compiled);

        private static readonly Color XmlDefaultColor = Color.Black;
        private static readonly Color XmlPunctuationColor = Color.Gray;
        private static readonly Color XmlTagColor = Color.Blue;
        private static readonly Color XmlAttributeColor = Color.FromArgb(163, 21, 21);
        private static readonly Color XmlValueColor = Color.Red;

        // SQL patterns
        private static readonly Regex SqlKeywordRegex = new Regex(
            @"\b(SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|CROSS|ON|AND|OR|NOT|IN|EXISTS|BETWEEN|LIKE|IS|NULL|AS|DISTINCT|TOP|ORDER|BY|GROUP|HAVING|UNION|ALL|CASE|WHEN|THEN|ELSE|END|CAST|CONVERT|COUNT|SUM|AVG|MIN|MAX|COALESCE|ISNULL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SqlStringRegex = new Regex(@"'[^']*'", RegexOptions.Compiled);
        private static readonly Regex SqlNumberRegex = new Regex(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex SqlCommentRegex = new Regex(@"--[^\r\n]*", RegexOptions.Compiled);
        private static readonly Regex SqlOperatorRegex = new Regex(@"[=<>!]+|[,;().]", RegexOptions.Compiled);

        private static readonly Color SqlDefaultColor = Color.Black;
        private static readonly Color SqlKeywordColor = Color.Blue;
        private static readonly Color SqlStringColor = Color.FromArgb(163, 21, 21);
        private static readonly Color SqlNumberColor = Color.DarkCyan;
        private static readonly Color SqlCommentColor = Color.Green;
        private static readonly Color SqlOperatorColor = Color.Gray;

        public void ColorizeXml(RichTextBox rtb, ref bool suppressFlag)
        {
            ColorizeRichTextBox(rtb, XmlDefaultColor, new (Regex, System.Func<Match, int>, System.Func<Match, int>, Color)[]
            {
                (XmlPunctuationRegex, m => m.Index, m => m.Length, XmlPunctuationColor),
                (XmlTagRegex, m => m.Index, m => m.Length, XmlTagColor),
                (XmlAttributeRegex, m => m.Groups[1].Index, m => m.Groups[1].Length, XmlAttributeColor),
                (XmlValueRegex, m => m.Index, m => m.Length, XmlValueColor),
            }, ref suppressFlag);
        }

        public void ColorizeSql(RichTextBox rtb, ref bool suppressFlag)
        {
            ColorizeRichTextBox(rtb, SqlDefaultColor, new (Regex, System.Func<Match, int>, System.Func<Match, int>, Color)[]
            {
                (SqlOperatorRegex, m => m.Index, m => m.Length, SqlOperatorColor),
                (SqlKeywordRegex, m => m.Index, m => m.Length, SqlKeywordColor),
                (SqlNumberRegex, m => m.Index, m => m.Length, SqlNumberColor),
                (SqlStringRegex, m => m.Index, m => m.Length, SqlStringColor),
                (SqlCommentRegex, m => m.Index, m => m.Length, SqlCommentColor),
            }, ref suppressFlag);
        }

        public void FormatAndColorizeXml(RichTextBox rtb, ref bool suppressFlag)
        {
            var xml = rtb.Text.Trim();
            if (string.IsNullOrEmpty(xml)) return;

            try
            {
                var doc = XDocument.Parse(xml);
                suppressFlag = true;
                rtb.Text = doc.ToString();
            }
            catch
            {
                // XML is invalid — colorize what's there
            }
            finally
            {
                suppressFlag = false;
            }

            ColorizeXml(rtb, ref suppressFlag);
        }

        public static void StripIncomingFormatting(RichTextBox rtb, ref bool suppressFlag)
        {
            var plain = rtb.Text;
            if (string.IsNullOrEmpty(plain)) return;

            suppressFlag = true;
            var pos = rtb.SelectionStart;
            rtb.SelectAll();
            rtb.SelectionBackColor = rtb.BackColor;
            rtb.SelectionFont = rtb.Font;
            rtb.SelectionStart = System.Math.Min(pos, plain.Length);
            rtb.SelectionLength = 0;
            suppressFlag = false;
        }

        private static void ColorizeRichTextBox(RichTextBox rtb, Color defaultColor,
            (Regex regex, System.Func<Match, int> getIndex, System.Func<Match, int> getLength, Color color)[] rules,
            ref bool suppressFlag)
        {
            var text = rtb.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            suppressFlag = true;
            var selStart = rtb.SelectionStart;
            var selLength = rtb.SelectionLength;

            rtb.SelectAll();
            rtb.SelectionColor = defaultColor;

            foreach (var (regex, getIndex, getLength, color) in rules)
            {
                foreach (Match m in regex.Matches(text))
                {
                    rtb.Select(getIndex(m), getLength(m));
                    rtb.SelectionColor = color;
                }
            }

            rtb.Select(selStart, selLength);
            rtb.SelectionColor = defaultColor;
            suppressFlag = false;
        }
    }
}
