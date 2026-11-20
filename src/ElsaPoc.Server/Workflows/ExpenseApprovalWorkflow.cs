using System.Net;
using Elsa.Extensions;
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;
using ElsaPoc.Server.Activities;
using ElsaPoc.Server.Models;

namespace ElsaPoc.Server.Workflows;

/// <summary>
/// A long-running, human-in-the-loop business process:
/// <list type="number">
/// <item>POST /api/workflows/expenses submits an expense claim.</item>
/// <item>Claims up to <see cref="AutoApproveLimit"/> are approved automatically.</item>
/// <item>Larger claims notify a manager and the workflow is persisted while it waits for an approve/reject event.</item>
/// <item>POST /demo/expenses/{instanceId}/approve|reject resumes the workflow.</item>
/// </list>
/// </summary>
public class ExpenseApprovalWorkflow : WorkflowBase
{
    public const decimal AutoApproveLimit = 1000;
    public const string ApprovedEvent = "ExpenseApproved";
    public const string RejectedEvent = "ExpenseRejected";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Expense Approval";
        builder.Description = $"Auto-approves expenses up to {AutoApproveLimit}; larger ones wait for a manager decision.";

        var expense = builder.WithVariable<ExpenseRequest>("Expense", new ExpenseRequest());
        var decision = builder.WithVariable<string>("Decision", "Pending");

        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/expenses"),
                    SupportedMethods = new(new[] { HttpMethods.Post }),
                    CanStartWorkflow = true,
                    ParsedContent = new(expense)
                },
                new WriteLine(context => $"Expense received: {expense.Get(context)!.Employee} claims {expense.Get(context)!.Amount:C} for '{expense.Get(context)!.Description}'"),
                new If(context => expense.Get(context)!.Amount <= AutoApproveLimit)
                {
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new SetVariable<string> { Variable = decision, Value = new("Auto-approved") },
                            new NotifyStakeholder
                            {
                                Recipient = new(context => expense.Get(context)!.Email),
                                Message = new(context => $"Your expense of {expense.Get(context)!.Amount:C} was auto-approved.")
                            },
                            new WriteHttpResponse
                            {
                                StatusCode = new(HttpStatusCode.OK),
                                Content = new(context => (object)new
                                {
                                    workflowInstanceId = context.GetWorkflowExecutionContext().Id,
                                    status = "AutoApproved",
                                    message = $"Amount is within the {AutoApproveLimit:C} auto-approval limit."
                                })
                            }
                        }
                    },
                    Else = new Sequence
                    {
                        Activities =
                        {
                            new NotifyStakeholder
                            {
                                Recipient = new("finance.manager@example.com"),
                                Message = new(context => $"Please review the {expense.Get(context)!.Amount:C} expense from {expense.Get(context)!.Employee}.")
                            },
                            new WriteHttpResponse
                            {
                                StatusCode = new(HttpStatusCode.Accepted),
                                Content = new(context =>
                                {
                                    var id = context.GetWorkflowExecutionContext().Id;
                                    return (object)new
                                    {
                                        workflowInstanceId = id,
                                        status = "PendingApproval",
                                        approveUrl = $"/demo/expenses/{id}/approve",
                                        rejectUrl = $"/demo/expenses/{id}/reject"
                                    };
                                })
                            },
                            // The workflow is persisted here and waits (for days if needed) until one of the events arrives.
                            new Fork
                            {
                                JoinMode = ForkJoinMode.WaitAny,
                                Branches =
                                {
                                    new Sequence
                                    {
                                        Activities =
                                        {
                                            new Event(ApprovedEvent),
                                            new SetVariable<string> { Variable = decision, Value = new("Approved") }
                                        }
                                    },
                                    new Sequence
                                    {
                                        Activities =
                                        {
                                            new Event(RejectedEvent),
                                            new SetVariable<string> { Variable = decision, Value = new("Rejected") }
                                        }
                                    }
                                }
                            },
                            new NotifyStakeholder
                            {
                                Recipient = new(context => expense.Get(context)!.Email),
                                Message = new(context => $"Your expense of {expense.Get(context)!.Amount:C} was {decision.Get(context)}.")
                            }
                        }
                    }
                },
                new WriteLine(context => $"Expense workflow finished. Decision: {decision.Get(context)}")
            }
        };
    }
}
