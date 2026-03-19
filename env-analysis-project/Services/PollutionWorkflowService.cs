using System;
using System.IO;
using System.Threading.Tasks;
using env_analysis_project.Models;

namespace env_analysis_project.Services
{
    public interface IPollutionWorkflowService
    {
        Task<ServiceResult<PredictionResult>> ProcessCsvAsync(Stream csvStream);
    }

    public sealed class PollutionWorkflowService : IPollutionWorkflowService
    {
        private readonly IPredictionService _predictionService;

        public PollutionWorkflowService(IPredictionService predictionService)
        {
            _predictionService = predictionService;
        }

        public async Task<ServiceResult<PredictionResult>> ProcessCsvAsync(Stream csvStream)
        {
            if (csvStream == null)
            {
                return ServiceResult<PredictionResult>.Fail("Vui lòng chọn một file CSV.");
            }

            var tempPath = Path.GetTempFileName();
            try
            {
                await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await csvStream.CopyToAsync(fileStream);
                }

                var result = _predictionService.UploadAndPredict(tempPath);
                return ServiceResult<PredictionResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<PredictionResult>.Fail($"Lỗi xử lý file hoặc dự đoán: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
