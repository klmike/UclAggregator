# UclAggregator

This is a small API that puts a Champions League report together from a few public sources. ESPN supplies the matches, standings and players. If ESPN cannot resolve a team name, TheSportsDB is the backup. Open-Meteo adds the weather at the venue around kickoff. News comes from Google, from ESPN when the query is about a team, and from The Guardian when a Guardian API key is set.

## Running it

You need the .NET SDK. From the project folder:

    dotnet run --launch-profile https

In Development that listens on https://localhost:7218 and http://localhost:5088. Scalar, the interactive docs, is at /scalar, and the OpenAPI document is at /openapi/v1.json.

Tests run on the host, not inside the image:

    dotnet test tests/UclAggregator.Tests

The container listens on port 8080. Compose publishes that as http://localhost:5088 and sets the environment to Development, so the development login below is what it accepts.

    docker compose up --build
    docker compose down

Guardian news in the container needs the key in the environment first: export GUARDIAN_API_KEY=..., then the same docker compose up --build.

Without Compose, the image is the same thing:

    docker build -t ucl-aggregator .
    docker run --rm -p 5088:8080 -e ASPNETCORE_ENVIRONMENT=Development ucl-aggregator

The app will not start if Jwt:SigningKey is shorter than 32 characters. A token lasts 30 minutes unless Jwt:ExpiryMinutes is changed. Guardian:ApiKey is optional; leave it empty and that news source is skipped. News searches always start with "Champions League", and a report returns 20 articles unless you pass limit. Successful calls to the upstream APIs are cached for 2 hours. Each of those calls times out after 10 seconds.

## Development login

appsettings.Development.json contains a single account:

- username: dev
- password: dev-password

Putting a username and password in a config file that is committed with the code is a bad practice. Anyone with the repository can log in, the password cannot be rotated without a code change, and there is no real user store behind it. It is here only because this is an assignment and the API needs something to call without setting up accounts. Login stays disabled if either value is empty. Do not reuse this account or the development signing key anywhere else.

Everything except login wants a bearer token.

    curl -k -X POST https://localhost:7218/auth/login \
      -H "Content-Type: application/json" \
      -d '{"username":"dev","password":"dev-password"}'

The response looks like { "accessToken": "eyJhbGciOi...", "expiresAtUtc": "2026-11-10T11:15:00Z" }. Send that token as Authorization: Bearer <accessToken> on the other requests.

## The API

All of the report endpoints are GET. Team names, player names and match ids in the URL should be URL-encoded. limit is optional and caps how many articles come back.

GET /news searches with the default keywords plus whatever you pass in keywords. from and to are dates in yyyy-MM-dd. You get { keywords, news[] }.

GET /teams/{name} is the team report. season defaults to the current season, which starts in July, so January still belongs to the previous year. You get { team, season, standing, matches[], topPlayers[], news[] }.

GET /matches/{id} loads one match. The id is match.id from a team or match response. GET /matches?home=&away= finds one when you do not have the id yet. date is there for when those two teams play more than once. Both return { match, stats[], weather, news[] }.

GET /players/{name} needs team. season defaults the same way as the team report. You get { team, season, player, news[] }.

GET /metrics/clients is the upstream calls. GET /metrics/api is this service's own routes. Both return latest and history, and each of those is split into fast (under 100 ms), average (100 to 200 ms) and slow (above that). Latest is the last five minutes and is there as soon as the calls happen. History is the five-minute snapshots a background job stores. Client numbers include real calls, cache hits, timeouts and errors. API numbers include the call count, the error rate and which error codes showed up.

Every five minutes that job also compares the window it just closed with the snapshots already stored. If the average time or the error rate is more than 50% above that baseline, it writes a warning. The first window has nothing to compare with, so it stays quiet.

Null fields are left out of the JSON. A failure comes back as { "error": { "code": 2000, "message": "..." } } and the code picks the HTTP status. A partial report can still be HTTP 200 with warnings, for example when the match was found but the weather could not be loaded. Articles are ordered by score, which is the source's own ranking times a recency weight (half-life of 7 days). The same title from two sources is kept once, whichever scored higher.

The error codes are 1000 invalid request (400), 1002 unauthorized (401), 1003 invalid credentials (401), 2000 team not found (404), 2001 match not found (404), 2002 player not found (404), 2003 data not available (500), 3000 provider unavailable (502), 3001 provider timeout (504), 3002 provider rate limited (503), 3003 provider bad response (502), and 9000 unexpected (500).
