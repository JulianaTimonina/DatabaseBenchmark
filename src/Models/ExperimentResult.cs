namespace DatabaseBenchmark.Models;

public class ExperimentResult {
    public string Database { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string QueryName { get; set; } = string.Empty;
    public int DatasetSize { get; set; }
    public int ReturnedRows { get; set; } // <--- НОВОЕ ПОЛЕ
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public double StdDevMs { get; set; }
}