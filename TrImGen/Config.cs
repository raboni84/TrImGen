namespace TrImGen
{
  public class Config
  {
    public string[] SourceDisks { get; set; }

    public TargetDiskType TargetDiskType { get; set; }

    public TargetPartitionType TargetPartitionType { get; set; }

    public long TargetDiskSize { get; set; }

    public string[] SearchPatterns { get; set; }

    public string[] EventHints { get; set; }

    public string[] EventSearchPatterns { get; set; }

    public string[] RegistryHints { get; set; }

    public string[] RegistrySearchPatterns { get; set; }

    public int CopyRetryCount { get; set; }
  }
}