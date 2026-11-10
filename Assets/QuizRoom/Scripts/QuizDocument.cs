using System;
using System.Globalization;
using System.Text;

namespace CampusCommons.Quiz
{
    public static class QuizText
    {
        // macOS filenames can contain decomposed Hangul. Normalize only visible text.
        public static string Display(string text) => (text ?? "").Normalize(NormalizationForm.FormC);
    }

    [Serializable]
    public class QuizPdfInspection
    {
        public long size_bytes;
        public int page_count;
        public long max_size_bytes;
        public int max_pages;
        public bool within_limits;
    }

    public sealed class QuizDocument
    {
        public const long MaxBytes = 10 * 1024 * 1024;
        public const int MaxPages = 80;
        public string Path { get; private set; } = "";
        public long Bytes { get; private set; }
        public int Pages { get; private set; }
        public string Warning { get; private set; } = "";
        public bool Inspected { get; private set; }
        public string Name => QuizText.Display(System.IO.Path.GetFileName(Path));
        public bool CanInspect => Path.Length > 0 && Bytes > 0 && Bytes <= MaxBytes;
        public bool CanGenerate => CanInspect && Inspected && Pages <= MaxPages && Warning.Length == 0;
        public string SizeLabel => Bytes >= 1024 * 1024
            ? (Bytes / (1024d * 1024)).ToString("0.00", CultureInfo.InvariantCulture) + " MB"
            : (Bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB";

        public void Select(string path, long bytes)
        {
            Reset(); Path = path ?? ""; Bytes = bytes;
            if (bytes <= 0) Warning = "비어 있는 파일입니다. 다른 PDF를 선택해 주세요.";
            else if (bytes > MaxBytes) Warning = "파일 용량이 10 MB 제한을 초과했습니다 (" + bytes.ToString("N0", CultureInfo.InvariantCulture) + "바이트). 용량을 줄인 PDF를 선택해 주세요.";
        }

        public bool ApplyInspection(QuizPdfInspection info)
        {
            Inspected = false; Pages = 0;
            if (!CanInspect || info == null || info.size_bytes != Bytes || info.page_count < 1 ||
                info.max_size_bytes != MaxBytes || info.max_pages != MaxPages ||
                info.within_limits != (info.page_count <= MaxPages))
            {
                Warning = "파일 정보를 확인하지 못했습니다. PDF를 다시 선택해 주세요.";
                return false;
            }
            Pages = info.page_count; Inspected = true;
            Warning = Pages > MaxPages ? "선택한 PDF는 " + Pages + "페이지로, 80페이지 제한을 초과했습니다." : "";
            return true;
        }

        public void Fail(string warning) { Inspected = false; Warning = warning; }
        public void Reset() { Path = ""; Bytes = 0; Pages = 0; Inspected = false; Warning = ""; }
    }
}
