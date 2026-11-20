using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace ElsaPoc.Server.Activities;

/// <summary>
/// A custom activity that simulates sending a notification (e.g. e-mail / Teams).
/// Because it lives in this assembly, it also appears in the Elsa Studio toolbox under "ElsaPoc".
/// </summary>
[Activity("ElsaPoc", "Notifications", "Sends a (simulated) notification to a stakeholder.")]
public class NotifyStakeholder : CodeActivity
{
    [Input(Description = "Who should receive the notification.")]
    public Input<string> Recipient { get; set; } = default!;

    [Input(Description = "The notification message.")]
    public Input<string> Message { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var logger = context.GetRequiredService<ILogger<NotifyStakeholder>>();
        logger.LogInformation("NOTIFICATION to {Recipient}: {Message}", Recipient.Get(context), Message.Get(context));
    }
}
