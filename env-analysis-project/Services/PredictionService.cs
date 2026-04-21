using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using env_analysis_project.Models;

namespace env_analysis_project.Services
{
    public class RegressionInput
    {
        public float Value { get; set; }
        public float TimeIndex { get; set; }
    }

    public class PollutionForecastResult
    {
        public float[] Forecast { get; set; } = Array.Empty<float>();
    }

    public class PredictionService : IPredictionService
    {
        private readonly MLContext _mlContext;
        private readonly ThresholdOptions _thresholds;

        public PredictionService(IOptions<ThresholdOptions> options)
        {
            _mlContext = new MLContext(seed: 0);
            _thresholds = options.Value;
        }
        private static readonly Dictionary<string, string> _paramMap = new(StringComparer.OrdinalIgnoreCase) {
            { "Bụi tổng", "TS05" },
            { "NOx", "TS07" },
            { "SO2", "TS08" },
            { "SO₂", "TS08" }
        };
        public PredictionResult UploadAndPredict(string filePath)
        {
            IDataView fullDataView = _mlContext.Data.LoadFromTextFile<PollutionData>(
                path: filePath, hasHeader: true, separatorChar: ',', allowQuoting: true);

            var rawList = _mlContext.Data
                .CreateEnumerable<PollutionData>(fullDataView, reuseRowObject: false)
                .ToList();

            return PredictFromData(rawList);
        }

        public PredictionResult PredictFromData(IEnumerable<PollutionData> data)
        {
            var result = new PredictionResult();
            var rawList = (data ?? Enumerable.Empty<PollutionData>())
                .Where(x => x != null &&
                            !string.IsNullOrEmpty(x.Parameter) &&
                            DateTime.TryParse(x.MeasurementDate, out _))
                .OrderBy(x => DateTime.Parse(x.MeasurementDate))
                .ToList();

            var parameters = rawList.Select(x => x.Parameter).Distinct().ToList();
            var totalR2 = 0d;
            var totalRMSE = 0d;
            var processedParams = 0;

            foreach (var pName in parameters)
            {
                var filtered = rawList
                    .Where(x => x.Parameter == pName)
                    .OrderBy(x => DateTime.Parse(x.MeasurementDate))
                    .ToList();

                if (filtered.Count < 5) continue;
                string pCode = _paramMap.GetValueOrDefault(pName, pName);
                float? threshold = _thresholds.ParameterCodes.ContainsKey(pCode) ? _thresholds.ParameterCodes[pCode] : null;
                //REGRESSION
                //TimeIndex 
                var regData = filtered.Select((x, index) => new RegressionInput
                {
                    Value = x.Value,
                    TimeIndex = (float)index
                }).ToList();

                
                var regDataView = _mlContext.Data.LoadFromEnumerable(regData);
                var regPipeline = _mlContext.Transforms.CopyColumns("Label", nameof(RegressionInput.Value))
                    .Append(_mlContext.Transforms.Concatenate("Features", nameof(RegressionInput.TimeIndex)))
                    .Append(_mlContext.Regression.Trainers.FastTree());
             
                var regModel = regPipeline.Fit(regDataView);
                var regEngine = _mlContext.Model.CreatePredictionEngine<RegressionInput, PollutionPrediction>(regModel);
                var regPredictions = regModel.Transform(regDataView);
                var metrics = _mlContext.Regression.Evaluate(regPredictions, labelColumnName: "Label");
                totalR2 += metrics.RSquared;
                totalRMSE += metrics.RootMeanSquaredError;
                processedParams++;
                //SPIKE DETECTION
                var pDataView = _mlContext.Data.LoadFromEnumerable(filtered);

  
                int pvalueHistoryLength = Math.Max(filtered.Count / 4, 3);
                int trainingWindowSize = Math.Max(pvalueHistoryLength + 1, Math.Min(filtered.Count, Math.Max(3, filtered.Count)));
                int seasonalityWindowSize = Math.Max(1, filtered.Count / 10);

                var spikePipeline = _mlContext.Transforms.DetectSpikeBySsa(
                    outputColumnName: nameof(SpikePredictionRow.SpikePrediction),
                    inputColumnName: nameof(PollutionData.Value),
                    confidence: 98.0d,
                    pvalueHistoryLength: pvalueHistoryLength,
                    trainingWindowSize: trainingWindowSize,
                    seasonalityWindowSize: seasonalityWindowSize,
                    side: AnomalySide.Positive);

                var spikeTransform = spikePipeline.Fit(pDataView).Transform(pDataView);
                var spikes = _mlContext.Data.CreateEnumerable<SpikePredictionRow>(spikeTransform, reuseRowObject: false).ToList();

                //SSA FORECASTING
                try
                {
                    int dynamicWindowSize = Math.Max(2, Math.Min(filtered.Count / 2, 10));
                    var forecastPipeline = _mlContext.Forecasting.ForecastBySsa(
                        outputColumnName: "Forecast",
                        inputColumnName: nameof(PollutionData.Value),
                        windowSize: dynamicWindowSize,
                        seriesLength: filtered.Count,
                        trainSize: filtered.Count,
                        horizon: 5);

                    var forecastModel = forecastPipeline.Fit(pDataView);
                    var forecastEngine = forecastModel.CreateTimeSeriesEngine<PollutionData, PollutionForecastResult>(_mlContext);
                    var forecastData = forecastEngine.Predict();

                    var futureList = new List<FutureForecast>();
                    var lastDate = filtered
                        .Select(x => DateTime.TryParse(x.MeasurementDate, out var d) ? d : DateTime.MinValue)
                        .Max();


                    for (int i = 0; i < forecastData.Forecast.Length; i++)
                    {
                        futureList.Add(new FutureForecast
                        {
                            Date = lastDate.AddMonths(i + 1), 
                            Value = forecastData.Forecast[i]
                        });
                    }
                    result.FutureForecasts[pName] = futureList;
                }
                catch
                {
                    result.FutureForecasts[pName] = new List<FutureForecast>();
                }

                for (int i = 0; i < filtered.Count; i++)
                {
                    var item = filtered[i];
                    DateTime.TryParse(item.MeasurementDate, out DateTime dt);
                    

                    var regPrediction = regEngine.Predict(new RegressionInput { TimeIndex = (float)i });

                    bool isSpike = false;
                    if (i < spikes.Count && spikes[i].SpikePrediction.Length > 0)
                        isSpike = spikes[i].SpikePrediction[0] == 1.0;

                    result.Rows.Add(new PredictionRow
                    {
                        ParameterDisplayName = pName,
                        MeasurementDate = dt,
                        ActualValue = item.Value,
                        PredictedValue = regPrediction.PredictedValue, 
                        IsWarning = threshold.HasValue && item.Value > threshold.Value,
                        IsSpike = isSpike,
                        Threshold = threshold
                    });
                }
            }

            result.Rows = result.Rows.OrderByDescending(x => x.MeasurementDate).ToList();
            result.WarningCount = result.Rows.Count(x => x.IsWarning);
            result.SpikeCount = result.Rows.Count(x => x.IsSpike);
            result.R2 = processedParams > 0 ? totalR2 / processedParams : 0;
            result.RMSE = processedParams > 0 ? totalRMSE / processedParams : 0;
            result.YearMonth = "Dữ liệu phân tích";
            return result;
        }
    }
}
