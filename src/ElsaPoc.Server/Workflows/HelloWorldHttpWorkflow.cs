using System.Net;
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace ElsaPoc.Server.Workflows;

/// <summary>
/// Simplest possible workflow: GET /api/workflows/hello?name=Bob returns a greeting.
/// </summary>
public class HelloWorldHttpWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Hello World (HTTP)";
        builder.Description = "Responds to GET /api/workflows/hello?name=YourName with a greeting.";

        var query = builder.WithVariable<IDictionary<string, object>>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new HttpEndpoint
                {
                    Path = new("/hello"),
                    SupportedMethods = new(new[] { HttpMethods.Get }),
                    CanStartWorkflow = true,
                    QueryStringData = new(query)
                },
                new WriteLine(context => $"Hello workflow triggered for '{GetName(query.Get(context))}'"),
                new WriteHttpResponse
                {
                    StatusCode = new(HttpStatusCode.OK),
                    Content = new(context => $"Hello {GetName(query.Get(context))}, this response was produced by an Elsa workflow!")
                }
            }
        };
    }

    private static string GetName(IDictionary<string, object>? query) =>
        query != null && query.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name?.ToString())
            ? name.ToString()!
            : "World";
}
