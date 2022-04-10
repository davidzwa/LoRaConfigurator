namespace LoraGateway.Models;

public class ExperimentPlotDto
{
    public double[] InputPerArray { get; set; }
    public double[] AveragePerArray { get; set; }
    public double[] ErrorArray { get; set; }
    
    public double[] GenSuccessRateArray { get; set; }
    // public uint[] GenSuccessArray { get; set; }
}