namespace UclAggregator.Clients.Guardian;

public sealed record GuardianResponse(GuardianBody? Response);

public sealed record GuardianBody(string? Status, string? Message, List<GuardianResult>? Results);

public sealed record GuardianResult(string? Id, string? WebTitle, string? WebUrl, string? WebPublicationDate);
