using System.Net;
using Elsa.Extensions;
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;
using ElsaPoc.Server.Activities;
using ElsaPoc.Server.Models;

namespace ElsaPoc.Server.Workflows;

public class PurchaseApprovalWorkflow : WorkflowBase
{
    public const decimal AutoApproveLimit = 1000;
    public const string ApprovedEvent = "PurchaseApproved";
    public const string RejectedEvent = "PurchaseRejected";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Equipment & Spares Procurement";
        builder.Description = "Demo purchasing approval for oil & gas sites; not an operational authorization.";
        var request = builder.WithVariable<PurchaseRequest>("Purchase", new PurchaseRequest());
        var decision = builder.WithVariable<string>("Decision", "PendingApproval");

        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/purchases"),
                    SupportedMethods = new(new[] { HttpMethods.Post }),
                    CanStartWorkflow = true,
                    ParsedContent = new(request)
                },
                new If(context => request.Get(context)?.IsValid() == true)
                {
                    Then = new Sequence
                    {
                        Activities =
                        {
                            Inline.From(context =>
                            {
                                var purchase = request.Get(context)!;
                                var output = context.WorkflowExecutionContext.Output;
                                output["requestedBy"] = purchase.RequestedBy;
                                output["email"] = purchase.Email;
                                output["site"] = purchase.Site;
                                output["equipment"] = purchase.Equipment;
                                output["estimatedCost"] = purchase.EstimatedCost;
                                output["justification"] = purchase.Justification;
                                output["decision"] = "PendingApproval";
                            }),
                            new If(context => request.Get(context)!.EstimatedCost <= AutoApproveLimit)
                            {
                                Then = new SetVariable<string> { Variable = decision, Value = new("AutoApproved") },
                                Else = new Sequence
                                {
                                    Activities =
                                    {
                                        new NotifyStakeholder
                                        {
                                            Recipient = new("procurement.manager@example.com"),
                                            Message = new(context => $"Review equipment purchase for {request.Get(context)!.Site}: {request.Get(context)!.Equipment}.")
                                        },
                                        new WriteHttpResponse
                                        {
                                            StatusCode = new(HttpStatusCode.Accepted),
                                            Content = new(context => (object)new
                                            {
                                                workflowInstanceId = context.GetWorkflowExecutionContext().Id,
                                                status = "PendingApproval"
                                            })
                                        },
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
                                        }
                                    }
                                }
                            },
                            Inline.From(context =>
                            {
                                context.WorkflowExecutionContext.Output["decision"] = decision.Get(context)!;
                            }),
                            new NotifyStakeholder
                            {
                                Recipient = new(context => request.Get(context)!.Email),
                                Message = new(context => $"Purchase of {request.Get(context)!.Equipment}: {decision.Get(context)} (demo only).")
                            },
                            new If(context => decision.Get(context) == "AutoApproved")
                            {
                                Then = new WriteHttpResponse
                                {
                                    StatusCode = new(HttpStatusCode.OK),
                                    Content = new(context => (object)new
                                    {
                                        workflowInstanceId = context.GetWorkflowExecutionContext().Id,
                                        status = "AutoApproved"
                                    })
                                }
                            }
                        }
                    },
                    Else = new WriteHttpResponse
                    {
                        StatusCode = new(HttpStatusCode.BadRequest),
                        Content = new((object)new { error = "Provide requester, valid email, site, equipment, justification, and a cost between USD 0.01 and 10,000,000." })
                    }
                }
            }
        };
    }
}
