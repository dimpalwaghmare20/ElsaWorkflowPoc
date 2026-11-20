using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using ElsaPoc.Server.Workflows;

namespace ElsaPoc.Server.Endpoints;

/// <summary>
/// Small, unauthenticated endpoints that make the approval step easy to demo from a browser/REST client.
/// </summary>
public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/demo/expenses");

        group.MapPost("/{instanceId}/{decision}", async (string instanceId, string decision, IEventPublisher eventPublisher, CancellationToken cancellationToken) =>
        {
            var eventName = decision.ToLowerInvariant() switch
            {
                "approve" => ExpenseApprovalWorkflow.ApprovedEvent,
                "reject" => ExpenseApprovalWorkflow.RejectedEvent,
                _ => null
            };

            if (eventName == null)
                return Results.BadRequest(new { error = "Decision must be 'approve' or 'reject'." });

            await eventPublisher.PublishAsync(eventName, workflowInstanceId: instanceId, cancellationToken: cancellationToken);
            return Results.Ok(new { workflowInstanceId = instanceId, eventPublished = eventName });
        });

        group.MapGet("/{instanceId}", async (string instanceId, IWorkflowInstanceStore store, CancellationToken cancellationToken) =>
        {
            var instance = await store.FindAsync(new WorkflowInstanceFilter { Id = instanceId }, cancellationToken);
            if (instance == null)
                return Results.NotFound();

            return Results.Ok(new
            {
                workflowInstanceId = instance.Id,
                workflowDefinition = instance.DefinitionId,
                status = instance.Status.ToString(),
                subStatus = instance.SubStatus.ToString(),
                createdAt = instance.CreatedAt,
                finishedAt = instance.FinishedAt
            });
        });
    }
}
