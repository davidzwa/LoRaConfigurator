namespace LoraGateway.Utils;

public class IqrDto
{
    public float Q1 { get; set; }
    public float Q3 { get; set; }
    public float IQR { get; set; }
    public float Median { get; set; }
}
public class Statistics
{
    /**
     * Function to give index of the median
     */
    public static float MedianIndex(int low, int high)
    {
        var n = (high - low) / 2.0f;
        return n + low;
    }

    /**
     * Function to calculate IQR
     */
    public static IqrDto Quartiles(float[] a)
    {
        Array.Sort(a);
        var isOdd = a.Length % 2 != 0;
        var n = a.Length - 1;
        
        var midIndex = MedianIndex(0, n);
        var high = (int)Math.Ceiling(midIndex);
        var low = (int)Math.Floor(midIndex);
        var median = (a[high] + a[low]) / 2;

        var offset = isOdd ? 1 : 0;
        var q1Index = MedianIndex(0, low - offset);
        var q1High = (int)Math.Ceiling(q1Index);
        var q1Low = (int)Math.Floor(q1Index);
        var q1 = (a[q1High] + a[q1Low]) / 2;

        var q3Index = MedianIndex(high + offset, n);
        var q3High = (int)Math.Ceiling(q3Index);
        var q3Low = (int)Math.Floor(q3Index);
        var q3 = (a[q3High] + a[q3Low]) / 2;

        // IQR calculation
        return new()
        {
            Q1 = q1,
            Median = median,
            Q3 = q3,
            IQR = q3 - q1
        };
    }
}