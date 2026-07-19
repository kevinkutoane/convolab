using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ConvoLab.Application.KnowledgeStudio;
using UglyToad.PdfPig;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class DocumentTextExtractorResolver(IEnumerable<IDocumentTextExtractor> extractors) : IDocumentTextExtractorResolver
{
    public IDocumentTextExtractor Resolve(string extension, string contentType) => extractors.FirstOrDefault(x => x.CanExtract(extension, contentType)) ?? throw new NotSupportedException($"No extractor supports '{extension}' ({contentType}).");
}

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string extension, string contentType) => extension is ".txt" or ".md" or ".markdown";
    public async Task<ExtractedKnowledgeDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen:true);
        var text = await reader.ReadToEndAsync(ct);
        var sections = text.Replace("\r","").Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select((x,i)=>new ExtractedSection(i==0?Path.GetFileNameWithoutExtension(fileName):null,null,x.Trim())).ToList();
        return new(Path.GetFileNameWithoutExtension(fileName), text, sections, []);
    }
}

public sealed class PdfTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string extension, string contentType) => extension == ".pdf";
    public Task<ExtractedKnowledgeDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var memory = new MemoryStream(); stream.CopyTo(memory); memory.Position=0;
        using var pdf = PdfDocument.Open(memory.ToArray());
        var sections = pdf.GetPages().Select(p=>new ExtractedSection($"Page {p.Number}",p.Number,p.Text)).Where(x=>!string.IsNullOrWhiteSpace(x.Text)).ToList();
        return Task.FromResult(new ExtractedKnowledgeDocument(Path.GetFileNameWithoutExtension(fileName), string.Join("\n\n", sections.Select(x=>x.Text)), sections, sections.Count==0?["No extractable text was found. The PDF may be scanned."]:[]));
    }
}

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string extension, string contentType) => extension == ".docx";
    public async Task<ExtractedKnowledgeDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX document.xml is missing.");
        using var reader = new StreamReader(entry.Open());
        var xml = await reader.ReadToEndAsync(ct);
        var paragraphs = Regex.Matches(xml, @"<w:p[\s\S]*?</w:p>").Select(m=>Regex.Replace(Regex.Replace(m.Value,@"<w:tab[^>]*/>","\t"),@"<[^>]+>","")).Select(System.Net.WebUtility.HtmlDecode).Where(x=>!string.IsNullOrWhiteSpace(x)).ToList();
        var sections=paragraphs.Select((x,i)=>new ExtractedSection(i==0?Path.GetFileNameWithoutExtension(fileName):null,null,x.Trim())).ToList();
        return new(Path.GetFileNameWithoutExtension(fileName), string.Join("\n\n",paragraphs), sections, []);
    }
}
