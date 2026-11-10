using System;
using System.Text;
using CampusCommons.Quiz;

public static class QuizDocumentTests
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static QuizPdfInspection Info(long bytes, int pages) => new QuizPdfInspection {
        size_bytes = bytes, page_count = pages, max_size_bytes = 10485760,
        max_pages = 80, within_limits = pages <= 80
    };
    public static int Main()
    {
        string decomposed = "분할정복 알고리즘.pdf".Normalize(NormalizationForm.FormD);
        Check(QuizText.Display(decomposed) == "분할정복 알고리즘.pdf", "Compose macOS Hangul for display");
        Check(QuizText.Display(null) == "", "Empty optional text is safe");
        var document = new QuizDocument();
        document.Select("/tmp/" + decomposed, 10485760);
        Check(document.Path == "/tmp/" + decomposed, "Preserve actual filesystem path");
        Check(document.Name == "분할정복 알고리즘.pdf", "Filename shown as composed Korean");
        Check(!document.CanGenerate && document.Pages == 0, "Wait for PDF inspection");
        Check(document.ApplyInspection(Info(10485760, 80)) && document.CanGenerate, "Accept exact size/page boundary");
        document.Select("/tmp/long.pdf", 2000);
        Check(document.Pages == 0 && !document.CanGenerate, "Reselection clears previous inspection");
        Check(document.ApplyInspection(Info(2000, 81)) && !document.CanGenerate, "81 pages must not generate");
        Check(document.Pages == 81 && document.Warning.Contains("81") && document.Warning.Contains("80"), "Show actual count and limit");
        document.Select("/tmp/large.pdf", 10485761);
        Check(!document.CanInspect && !document.CanGenerate && document.Warning.Contains("10 MB"), "Block oversized file before upload");
        document.Select("/tmp/empty.pdf", 0);
        Check(!document.CanInspect && !document.CanGenerate, "Reject empty file");
        document.Select("/tmp/valid.pdf", 2000);
        Check(!document.ApplyInspection(Info(1999, 3)) && !document.CanGenerate, "Reject metadata for different bytes");
        document.Select("/tmp/valid.pdf", 2000);
        Check(!document.ApplyInspection(Info(2000, 0)), "Reject malformed zero-page metadata");
        document.Select("/tmp/valid.pdf", 2000);
        var inconsistent = Info(2000, 81); inconsistent.within_limits = true;
        Check(!document.ApplyInspection(inconsistent), "Do not trust inconsistent allowed flag");
        document.Select("/tmp/valid.pdf", 2000);
        Check(document.ApplyInspection(Info(2000, 3)) && document.CanGenerate, "Valid new PDF recovers after an error");
        document.Reset();
        Check(document.Path == "" && document.Pages == 0 && !document.CanGenerate, "Exit clears document state");
        Console.WriteLine("QUIZ_DOCUMENT_PASS " + checks + " checks");
        return 0;
    }
}
