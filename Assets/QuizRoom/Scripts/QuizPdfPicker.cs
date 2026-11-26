using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CampusCommons.Quiz
{
    internal static class QuizPdfPicker
    {
        // Called from Unity's main thread. A dismissed panel returns null.
        public static Task<string> OpenAsync(string directory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
            return OpenMacAsync(directory, cancellationToken);
#elif UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return QuizWindowsPdfPicker.OpenAsync(directory, cancellationToken);
#elif UNITY_EDITOR
            return Task.FromResult(UnityEditor.EditorUtility.OpenFilePanel("공부한 PDF 선택", directory, "pdf"));
#else
            throw new PlatformNotSupportedException("이 플랫폼에서는 PDF 파일 선택을 지원하지 않습니다.");
#endif
        }

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
        const string Library = "CampusQuizPdf";

        [DllImport(Library)]
        static extern IntPtr CCQuizPdfOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string directory);
        [DllImport(Library)]
        static extern int CCQuizPdfPoll(IntPtr request, out IntPtr selectedPath);
        [DllImport(Library)]
        static extern void CCQuizPdfClose(IntPtr request);

        static async Task<string> OpenMacAsync(string directory, CancellationToken cancellationToken)
        {
            IntPtr request = CCQuizPdfOpen(directory);
            if (request == IntPtr.Zero) throw new InvalidOperationException("PDF 선택 창을 열 수 없습니다.");
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CCQuizPdfPoll(request, out IntPtr path) != 0)
                        return path == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(path);
                    await Task.Delay(75, cancellationToken);
                }
            }
            finally
            {
                CCQuizPdfClose(request);
            }
        }
#endif
    }
}
