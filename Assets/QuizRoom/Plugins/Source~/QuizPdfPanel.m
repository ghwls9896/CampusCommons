// Rebuild inside Plugins with the macOS Command Line Tools:
// clang -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=12.0 -fobjc-arc -framework AppKit -framework UniformTypeIdentifiers -install_name @rpath/libCampusQuizPdf.dylib Source~/QuizPdfPanel.m -o libCampusQuizPdf.dylib
#import <AppKit/AppKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

@interface CCQuizPdfRequest : NSObject
@property (strong) NSOpenPanel *panel;
@property (strong) NSData *selection;
@property BOOL finished;
@end
@implementation CCQuizPdfRequest
@end

// Unity calls these functions on its main thread. Each request has a distinct
// handle so completion of a cancelled sheet cannot affect a later visit.
void *CCQuizPdfOpen(const char *directory)
{
    NSWindow *owner = NSApp.keyWindow ?: NSApp.mainWindow;
    if (!owner || owner.attachedSheet) return NULL;

    CCQuizPdfRequest *request = [CCQuizPdfRequest new];
    NSOpenPanel *panel = [NSOpenPanel openPanel];
    panel.title = @"공부한 PDF 선택";
    panel.allowedContentTypes = @[UTTypePDF];
    panel.canChooseDirectories = NO;
    panel.canChooseFiles = YES;
    panel.allowsMultipleSelection = NO;
    if (directory && directory[0]) {
        NSString *path = [[NSFileManager defaultManager]
            stringWithFileSystemRepresentation:directory length:strlen(directory)];
        if (path) panel.directoryURL = [NSURL fileURLWithPath:path isDirectory:YES];
    }
    request.panel = panel;
    [panel beginSheetModalForWindow:owner completionHandler:^(NSModalResponse response) {
        if (response == NSModalResponseOK) {
            const char *path = request.panel.URL.fileSystemRepresentation;
            if (path) request.selection = [NSData dataWithBytes:path length:strlen(path) + 1];
        }
        request.finished = YES;
        request.panel = nil;
    }];
    return (__bridge_retained void *)request;
}

int CCQuizPdfPoll(void *handle, const char **selection)
{
    CCQuizPdfRequest *request = (__bridge CCQuizPdfRequest *)handle;
    *selection = request.selection.bytes;
    return request.finished;
}

void CCQuizPdfClose(void *handle)
{
    CCQuizPdfRequest *request = (__bridge_transfer CCQuizPdfRequest *)handle;
    if (!request.finished) [request.panel cancel:nil];
}
