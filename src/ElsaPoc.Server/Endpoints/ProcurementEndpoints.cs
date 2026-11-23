using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using ElsaPoc.Server.Workflows;

namespace ElsaPoc.Server.Endpoints;

public static class ProcurementEndpoints
{
    public static void MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/demo/purchases");

        group.MapGet("/", async (IWorkflowInstanceStore store, CancellationToken cancellationToken) =>
        {
            var instances = await store.FindManyAsync(
                new WorkflowInstanceFilter { DefinitionId = nameof(PurchaseApprovalWorkflow) }, cancellationToken);
            return Results.Ok(instances
                .Where(instance => instance.WorkflowState.Output.ContainsKey("equipment"))
                .OrderByDescending(instance => instance.CreatedAt)
                .Select(instance => new
                {
                    workflowInstanceId = instance.Id,
                    status = instance.Status.ToString(),
                    subStatus = instance.SubStatus.ToString(),
                    createdAt = instance.CreatedAt,
                    details = instance.WorkflowState.Output,
                    canDecide = instance.SubStatus.ToString() == "Suspended"
                        && instance.WorkflowState.Output.TryGetValue("decision", out var decision)
                        && decision?.ToString() == "PendingApproval"
                }));
        });

        group.MapPost("/{instanceId}/{decision}", async (
            string instanceId, string decision, IWorkflowInstanceStore store,
            IEventPublisher publisher, CancellationToken cancellationToken) =>
        {
            var eventName = decision switch
            {
                "approve" => PurchaseApprovalWorkflow.ApprovedEvent,
                "reject" => PurchaseApprovalWorkflow.RejectedEvent,
                _ => null
            };
            if (eventName == null)
                return Results.BadRequest(new { error = "Decision must be approve or reject." });

            var instance = await store.FindAsync(new WorkflowInstanceFilter
            {
                Id = instanceId,
                DefinitionId = nameof(PurchaseApprovalWorkflow)
            }, cancellationToken);
            if (instance == null)
                return Results.NotFound(new { error = "Purchase workflow not found." });
            if (instance.SubStatus.ToString() != "Suspended"
                || !instance.WorkflowState.Output.TryGetValue("decision", out var current)
                || current?.ToString() != "PendingApproval")
                return Results.Conflict(new { error = "This purchase is not awaiting a manager decision. Refresh its status." });

            await publisher.PublishAsync(eventName, workflowInstanceId: instanceId, cancellationToken: cancellationToken);
            return Results.Accepted(value: new { workflowInstanceId = instanceId, message = "Decision sent to Elsa. Refresh to confirm completion." });
        });
    }
}
