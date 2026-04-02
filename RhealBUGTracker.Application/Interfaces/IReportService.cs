using RhealBUGTracker.Domain.Models;

namespace RhealBUGTracker.Application.Interfaces;

public interface IReportService
{
    string GenerateMarkdownReport(ScanSession session);
    object GenerateJsonReport(ScanSession session);
}
