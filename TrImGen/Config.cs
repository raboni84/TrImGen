namespace TrImGen
{
  public class Config
  {
    public string[] SourceDisks { get; set; }

    public TargetDiskType TargetDiskType { get; set; }

    public TargetPartitionType TargetPartitionType { get; set; }

    public long TargetDiskSize { get; set; }

    public string[] SearchPatterns { get; set; }
  }
}