namespace SarhSummarizer.Services;

public class TextChunker
{
    private const int MaxChunkWords = 3000;

    public List<string> ChunkText(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= MaxChunkWords)
        {
            return new List<string> { text };
        }

        var chunks = new List<string>();
        // Rule 3: Split on paragraph boundaries (\n\n) first, then sentence boundaries
        // For simplicity in this assessment, we'll implement a greedy chunker that respects these boundaries where possible.
        
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new System.Text.StringBuilder();
        int currentWordCount = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphWords = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            if (currentWordCount + paragraphWords > MaxChunkWords && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                currentWordCount = 0;
            }

            // If a single paragraph is larger than MaxChunkWords, we need to split it further (sentences)
            if (paragraphWords > MaxChunkWords)
            {
                var sentences = paragraph.Split(new[] { ". ", "? ", "! " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var sentence in sentences)
                {
                    var sentenceWords = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    if (currentWordCount + sentenceWords > MaxChunkWords && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                        currentWordCount = 0;
                    }
                    currentChunk.Append(sentence).Append(". ");
                    currentWordCount += sentenceWords;
                }
            }
            else
            {
                currentChunk.Append(paragraph).Append("\n\n");
                currentWordCount += paragraphWords;
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }
}
