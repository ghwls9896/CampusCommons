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
        public bool supports_page_ranges;
        public int max_selected_pages;
    }

    public sealed class QuizDocument
    {
        public const long MaxBytes = 10 * 1024 * 1024;
        public const int MaxPages = 1000;
        public string Path { get; private set; } = "";
        public long Bytes { get; private set; }
        public int Pages { get; private set; }
        string fileWarning="";
        public string StartText {get;private set;}="";
        public string EndText {get;private set;}="";
        public int StartPage {get {int n;return int.TryParse(StartText,NumberStyles.None,CultureInfo.InvariantCulture,out n)?n:0;}}
        public int EndPage {get {int n;return int.TryParse(EndText,NumberStyles.None,CultureInfo.InvariantCulture,out n)?n:0;}}
        public int SelectedPages=>StartPage>0 && EndPage>=StartPage?EndPage-StartPage+1:0;
        public string Warning {get {
            if(fileWarning.Length>0) return fileWarning;
            if(!Inspected) return "";
            if(StartPage<1 || EndPage<StartPage || EndPage>Pages) return "1~"+Pages+"페이지 안에서 시작·끝 페이지를 확인해 주세요.";
            if(SelectedPages>MaxPages) return "한 번에 선택할 수 있는 범위는 1000페이지까지입니다.";
            return "";
        }}
        public void SetRange(string first,string last) {StartText=first??"";EndText=last??"";}
        public bool Inspected { get; private set; }
        public string Name => QuizText.Display(System.IO.Path.GetFileName(Path));
        public bool CanInspect => Path.Length > 0 && Bytes > 0 && Bytes <= MaxBytes;
        public bool CanGenerate => CanInspect && Inspected && Warning.Length == 0;
        public string SizeLabel => Bytes >= 1024 * 1024
            ? (Bytes / (1024d * 1024)).ToString("0.00", CultureInfo.InvariantCulture) + " MB"
            : (Bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB";

        public void Select(string path, long bytes)
        {
            Reset(); Path = path ?? ""; Bytes = bytes;
            if (bytes <= 0) fileWarning = "비어 있는 파일입니다. 다른 PDF를 선택해 주세요.";
            else if (bytes > MaxBytes) fileWarning = "파일 용량이 10 MB 제한을 초과했습니다 (" + bytes.ToString("N0", CultureInfo.InvariantCulture) + "바이트). 용량을 줄인 PDF를 선택해 주세요.";
        }

        public bool ApplyInspection(QuizPdfInspection info)
        {
            Inspected = false; Pages = 0;
            if (!CanInspect || info == null || info.size_bytes != Bytes || info.page_count < 1 ||
                info.max_size_bytes != MaxBytes || !info.supports_page_ranges || info.max_selected_pages != MaxPages || !info.within_limits)
            {
                fileWarning = "파일 정보를 확인하지 못했습니다. PDF를 다시 선택해 주세요.";
                return false;
            }
            Pages = info.page_count; Inspected = true;
            fileWarning="";SetRange("1",Math.Min(Pages,MaxPages).ToString(CultureInfo.InvariantCulture));
            return true;
        }

        public void Fail(string warning) { Inspected = false; fileWarning = warning; }
        public void Reset() { Path = ""; Bytes = 0; Pages = 0; Inspected = false; fileWarning = "";StartText=EndText=""; }
    }
}
