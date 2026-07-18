using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBenchmark.Core;

public static class StatisticsHelper {
    public static double GetMedian(List<double> values) {
        if (values == null || values.Count == 0) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        int count = sorted.Count;
        
        if (count % 2 == 0) {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        return sorted[count / 2];
    }

    public static double GetStandardDeviation(List<double> values, double mean) {
        if (values == null || values.Count <= 1) return 0;
        
        double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }
}