using HealthTrack.Application.Persistence;
using HealthTrack.Application.Security;
using HealthTrack.Domain.Health;
using HealthTrack.Domain.Profiles;

namespace HealthTrack.Application.Health;

public sealed record MeasurementRequest(decimal WeightKg, decimal? HeightCm, DateTime RecordedAtUtc);
public sealed record MeasurementView(int Id, decimal WeightKg, decimal? HeightCm, DateTime RecordedAtUtc, decimal? Bmi);
public sealed record FeedbackRequest(string ClientUserId, string Text);
public sealed record FeedbackView(int Id, string TrainerUserId, string Text, DateTime CreatedAtUtc);

public sealed class HealthService(IDataSession data, ClientAccess access, TimeProvider clock)
{
    public async Task<int> RecordAsync(MeasurementRequest request, CancellationToken ct = default)
    {
        var actor = await access.ClientAsync(ct);
        Input.Decimal(request.WeightKg, 0.01m, 9999.99m, "Weight");
        if (request.HeightCm.HasValue) Input.Decimal(request.HeightCm.Value, 0.01m, 9999.99m, "Height");
        Input.EventTime(request.RecordedAtUtc, clock);
        var profile = await data.SingleAsync(data.Query<ClientProfile>().Where(x => x.UserId == actor.UserId), ct)
            ?? throw RequestFailure.Missing();
        var measurement = new HealthMeasurement
        {
            ClientUserId = actor.UserId, WeightKg = request.WeightKg,
            HeightCm = request.HeightCm ?? profile.HeightCm, RecordedAtUtc = request.RecordedAtUtc
        };
        data.Add(measurement);
        await data.SaveAsync(ct);
        return measurement.Id;
    }

    public async Task<IReadOnlyList<MeasurementView>> HistoryAsync(string clientId, DateTime from, DateTime to, int page, int size, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.DateRange(from, to); Input.Page(page, size);
        var rows = await data.ListAsync(data.Query<HealthMeasurement>().Where(x => x.ClientUserId == clientId && x.RecordedAtUtc >= from && x.RecordedAtUtc < to)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size), ct);
        return rows.Select(x => new MeasurementView(x.Id, x.WeightKg, x.HeightCm, x.RecordedAtUtc,
            HealthCalculations.BodyMassIndex(x.WeightKg, x.HeightCm))).ToArray();
    }

    public async Task<int> AddFeedbackAsync(FeedbackRequest request, CancellationToken ct = default)
    {
        Input.Text(request.ClientUserId, 128, "Client ID");
        var text = Input.Text(request.Text, 2000, "Feedback");
        return await data.SerializeAsync(["client:" + request.ClientUserId], async () =>
        {
            var actor = await access.TrainerWriteAsync(request.ClientUserId, ct);
            var feedback = new TrainerFeedback { ClientUserId = request.ClientUserId, TrainerUserId = actor.UserId, Text = text, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            data.Add(feedback);
            await data.SaveAsync(ct);
            return feedback.Id;
        }, ct);
    }

    public async Task<IReadOnlyList<FeedbackView>> FeedbackAsync(string clientId, int page, int size, CancellationToken ct = default)
    {
        await access.ReadAsync(clientId, ct); Input.Page(page, size);
        return await data.ListAsync(data.Query<TrainerFeedback>().Where(x => x.ClientUserId == clientId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new FeedbackView(x.Id, x.TrainerUserId, x.Text, x.CreatedAtUtc)), ct);
    }
}
