namespace ShootSorter.Core.Metadata;

public interface IExifDateReader
{
    /// <summary>
    /// 讀取 EXIF 拍攝時間（本機時間）。格式無法辨識或沒有日期時回傳 null；檔案本身無法存取時拋出 IOException。
    /// </summary>
    DateTime? ReadCaptureDate(string path);
}
