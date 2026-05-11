using HtmlAgilityPack;
using System.Text;

namespace ChildrenCharity.Mailing.Helper
{
    /// <summary>
    /// Конвертирует HTML в формат, совместимый с WP (форматирование применяется только к целым словам).
    /// Рекурсивно проходит по узлам дерева DOM, накапливая стили и добавляя фрагменты текста в лист.
    /// В конце из соответствия фрагментов текста и стилей применяет разметку Markdown WP.
    /// </summary>
    public static class HtmlToWPHelper
    {
        private static readonly Dictionary<string, StyleFlags> TagToStyle = new()
        {
            ["strong"] = StyleFlags.Bold,
            ["b"] = StyleFlags.Bold,
            ["em"] = StyleFlags.Italic,
            ["i"] = StyleFlags.Italic,
            ["del"] = StyleFlags.Strikethrough,
            ["s"] = StyleFlags.Strikethrough,
            ["strike"] = StyleFlags.Strikethrough,
            ["code"] = StyleFlags.Code,
            ["pre"] = StyleFlags.Code
        };
    
        private static bool IsWordPart(char c) => !(c == '\n' || c == ' ');
    
        [Flags]
        private enum StyleFlags
        {
            None = 0,
            Bold = 1,
            Italic = 2,
            Strikethrough = 4,
            Code = 8
        }
    
        private class TextFragment
        {
            public string Text { get; set; }
            public StyleFlags Style { get; set; }
        }
    
        public static string Convert(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;
    
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
    
            var fragments = new List<TextFragment>();
            Traverse(doc.DocumentNode, StyleFlags.None, fragments);

            var textResult = BuildText(fragments);

            return "\n" + textResult.Replace("&nbsp;", " ").Trim();
        }
    
        private static void Traverse(HtmlNode node, StyleFlags currentStyle, List<TextFragment> fragments)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Text)
                {
                    string text = child.InnerText;
                    if (!string.IsNullOrEmpty(text))
                        fragments.Add(new TextFragment { Text = text, Style = currentStyle });
                }
                else
                {
                    if (child.Name.Equals("p") && child.Closed)
                    {
                        fragments.Add(new TextFragment { Text = "\n", Style = StyleFlags.None });
                    }
    
                    StyleFlags newStyle = currentStyle;
                    if (TagToStyle.TryGetValue(child.Name, out var style))
                        newStyle |= style;
    
                    Traverse(child, newStyle, fragments);
                }
            }
        }
    
        private static string BuildText(List<TextFragment> fragments)
        {
            var result = new StringBuilder();
            var currentWord = new StringBuilder();
            StyleFlags wordStyle = StyleFlags.None;
    
            void FlushWord()
            {
                if (currentWord.Length == 0) return;
    
                string word = currentWord.ToString();
                if (wordStyle != StyleFlags.None)
                    word = ApplyStyles(word, wordStyle);
    
                result.Append(word);
                currentWord.Clear();
                wordStyle = StyleFlags.None;
            }
    
            foreach (var frag in fragments)
            {
                foreach (char c in frag.Text)
                {
                    if (IsWordPart(c))
                    {
                        currentWord.Append(c);
                        wordStyle |= frag.Style;
                    }
                    else
                    {
                        FlushWord();
                        result.Append(c);
                    }
                }
            }
            FlushWord();

            return result.ToString(); 
        }
    
        private static string ApplyStyles(string word, StyleFlags styles)
        {
            if (styles == StyleFlags.None) return word;
    
            var open = new StringBuilder();
            var close = new StringBuilder();
    
            if (styles.HasFlag(StyleFlags.Bold)) { open.Append('*'); close.Insert(0, '*'); }
            if (styles.HasFlag(StyleFlags.Italic)) { open.Append('_'); close.Insert(0, '_'); }
            if (styles.HasFlag(StyleFlags.Strikethrough)) { open.Append('~'); close.Insert(0, '~'); }
            if (styles.HasFlag(StyleFlags.Code)) { open.Append('`'); close.Insert(0, '`'); }
    
            return open.ToString() + word + close.ToString();
        }
    }
}
