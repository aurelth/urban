using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Urban.IntegrationTests.Api;

/// <summary>Um mês fechado de verdade pela API: duas unidades (frações 0,6 e 0,4) e uma despesa fixa de R$ 1.000,00, o que dá cobranças de 600 e 400.</summary>
internal static class ClosedMonthKit
{
    public static string Month(int offset)
    {
        DateTimeOffset brasilia = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        DateTime shifted = new DateTime(brasilia.Year, brasilia.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(offset);

        return shifted.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    public static string Today(int daysFromToday = 0) =>
        DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).DateTime).AddDays(daysFromToday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static async Task CloseAsync(HttpClient client, ManagerSession manager, string month, string dueDate)
    {
        await SendAsync(client, manager, HttpMethod.Post, "/api/units", new JsonObject { ["number"] = "101", ["type"] = "apartment", ["idealFraction"] = 0.6m }, HttpStatusCode.Created);
        await SendAsync(client, manager, HttpMethod.Post, "/api/units", new JsonObject { ["number"] = "102", ["type"] = "apartment", ["idealFraction"] = 0.4m }, HttpStatusCode.Created);
        JsonElement group = await SendAsync(client, manager, HttpMethod.Post, "/api/finance/expense-groups", new JsonObject { ["name"] = "Contratos" }, HttpStatusCode.Created);
        await SendAsync(
            client,
            manager,
            HttpMethod.Post,
            "/api/finance/expenses",
            new JsonObject
            {
                ["kind"] = "fixed",
                ["groupId"] = group.GetProperty("id").GetGuid(),
                ["description"] = "Portaria",
                ["startMonth"] = month,
                ["amount"] = 1000m,
                ["dueDay"] = 10,
            },
            HttpStatusCode.Created);
        await SendAsync(client, manager, HttpMethod.Post, $"/api/finance/months/{month}/open", new JsonObject(), HttpStatusCode.OK);
        await SendAsync(client, manager, HttpMethod.Post, $"/api/finance/months/{month}/close", new JsonObject { ["dueDate"] = dueDate }, HttpStatusCode.OK);
    }

    private static async Task<JsonElement> SendAsync(HttpClient client, ManagerSession manager, HttpMethod method, string route, JsonObject body, HttpStatusCode expected)
    {
        HttpResponseMessage response = await client.SendJsonAsync(method, route, manager.AccessToken, body);
        response.StatusCode.Should().Be(expected, $"{method} {route}: {await response.Content.ReadAsStringAsync()}");

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
