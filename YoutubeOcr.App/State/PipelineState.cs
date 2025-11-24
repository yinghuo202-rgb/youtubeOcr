using System.Collections.ObjectModel;
using YoutubeOcr.Core.Config;
using YoutubeOcr.Core.Models;

namespace YoutubeOcr.App;

public class PipelineState
{
    public ObservableCollection<VideoInfo> Videos { get; } = new();
    public ObservableCollection<FrameInfo> Frames { get; } = new();
    public ObservableCollection<OcrResult> OcrResults { get; } = new();
    public PipelineConfig Config { get; set; } = new();
}
