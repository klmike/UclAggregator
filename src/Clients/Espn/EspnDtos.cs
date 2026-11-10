using System.Text.Json;
using System.Text.Json.Serialization;

namespace UclAggregator.Clients.Espn;

public sealed class EspnEventDto
{
    public string? Id { get; init; }
    public string? Date { get; init; }
    public StatusDto? Status { get; init; }
    public SeasonTypeDto? SeasonType { get; init; }
    public List<CompetitionDto>? Competitions { get; init; }

    public sealed class StatusDto { public TypeDto? Type { get; init; } }
    public sealed class TypeDto { public string? Description { get; init; } }
    public sealed class SeasonTypeDto { public string? Name { get; init; } }

    public sealed class CompetitionDto
    {
        public string? Id { get; init; }
        public string? Date { get; init; }
        public StatusDto? Status { get; init; }
        public List<CompetitorDto>? Competitors { get; init; }
        public VenueDto? Venue { get; init; }
    }

    public sealed class CompetitorDto
    {
        public string? HomeAway { get; init; }
        public TeamDto? Team { get; init; }

        [JsonConverter(typeof(ScoreConverter))]
        public string? Score { get; init; }
    }

    public sealed class TeamDto { public string? DisplayName { get; init; } }

    public sealed class VenueDto
    {
        public string? FullName { get; init; }
        public AddressDto? Address { get; init; }
    }

    public sealed class AddressDto
    {
        public string? City { get; init; }
        public string? Country { get; init; }
    }

    private sealed class ScoreConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var element = JsonElement.ParseValue(ref reader);
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.TryGetProperty("displayValue", out var d) ? d.ToString() : null,
                JsonValueKind.Null => null,
                _ => element.ToString(),
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}

public sealed class EspnScoreboardDto
{
    public List<EspnEventDto>? Events { get; init; }
}

public sealed class EspnScheduleDto
{
    public List<EspnEventDto>? Events { get; init; }
}

public sealed class EspnSummaryDto
{
    public EspnEventDto? Header { get; init; }
    public GameInfoDto? GameInfo { get; init; }
    public BoxscoreDto? Boxscore { get; init; }

    public sealed class GameInfoDto { public EspnEventDto.VenueDto? Venue { get; init; } }

    public sealed class BoxscoreDto { public List<TeamStatsDto>? Teams { get; init; } }

    public sealed class TeamStatsDto
    {
        public EspnEventDto.TeamDto? Team { get; init; }
        public List<StatDto>? Statistics { get; init; }
    }

    public sealed class StatDto
    {
        public string? Name { get; init; }
        public string? DisplayValue { get; init; }
    }
}

public sealed class EspnStandingsDto
{
    public List<GroupDto>? Children { get; init; }

    public sealed class GroupDto
    {
        public string? Name { get; init; }
        public TableDto? Standings { get; init; }
    }

    public sealed class TableDto { public List<EntryDto>? Entries { get; init; } }

    public sealed class EntryDto
    {
        public TeamDto? Team { get; init; }
        public List<StatDto>? Stats { get; init; }
        public NoteDto? Note { get; init; }
    }

    public sealed class TeamDto
    {
        public string? Id { get; init; }
        public string? DisplayName { get; init; }
    }

    public sealed class StatDto
    {
        public string? Name { get; init; }
        public double? Value { get; init; }
    }

    public sealed class NoteDto { public string? Description { get; init; } }
}

public sealed class EspnRosterDto
{
    public List<AthleteDto>? Athletes { get; init; }

    public sealed class AthleteDto
    {
        public string? Id { get; init; }
        public string? DisplayName { get; init; }
        public PositionDto? Position { get; init; }
        public StatisticsDto? Statistics { get; init; }
    }

    public sealed class PositionDto { public string? DisplayName { get; init; } }

    public sealed class StatisticsDto { public SplitsDto? Splits { get; init; } }

    public sealed class SplitsDto { public List<CategoryDto>? Categories { get; init; } }

    public sealed class CategoryDto { public List<StatDto>? Stats { get; init; } }

    public sealed class StatDto
    {
        public string? Name { get; init; }
        public double? Value { get; init; }
    }
}

public sealed class EspnNewsDto
{
    public List<ArticleDto>? Articles { get; init; }

    public sealed class ArticleDto
    {
        public string? Id { get; init; }
        public string? Headline { get; init; }
        public string? Published { get; init; }
        public LinksDto? Links { get; init; }
    }

    public sealed class LinksDto { public LinkDto? Web { get; init; } }

    public sealed class LinkDto { public string? Href { get; init; } }
}

public sealed class EspnTeamsDto
{
    public List<SportDto>? Sports { get; init; }

    public sealed class SportDto { public List<LeagueDto>? Leagues { get; init; } }
    public sealed class LeagueDto { public List<TeamEntryDto>? Teams { get; init; } }
    public sealed class TeamEntryDto { public TeamDto? Team { get; init; } }

    public sealed class TeamDto
    {
        public string? Id { get; init; }
        public string? DisplayName { get; init; }
        public string? ShortDisplayName { get; init; }
        public string? Name { get; init; }
        public string? Location { get; init; }
        public string? Abbreviation { get; init; }
    }
}
