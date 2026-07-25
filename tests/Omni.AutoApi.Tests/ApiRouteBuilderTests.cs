using Omni.AutoApi.Routing;
using Xunit;

namespace Omni.AutoApi.Tests;

public class ApiRouteBuilderTests
{
    [Theory]
    [InlineData("GetTodosAsync", "GET")]
    [InlineData("CreateTodoAsync", "POST")]
    [InlineData("UpdateTodoAsync", "PUT")]
    [InlineData("DeleteTodoAsync", "DELETE")]
    [InlineData("PatchTodoAsync", "PATCH")]
    [InlineData("DoSomethingAsync", "POST")]
    public void GetHttpMethod_maps_prefix(string method, string expected)
        => Assert.Equal(expected, ApiRouteBuilder.GetHttpMethod(method));

    [Fact]
    public void GetActionName_uses_kebab_by_default()
        => Assert.Equal("get-todos", ApiRouteBuilder.GetActionName("GetTodosAsync"));

    [Fact]
    public void GetActionName_uses_camel_when_kebab_disabled()
        => Assert.Equal("getTodos", ApiRouteBuilder.GetActionName("GetTodosAsync", new RouteOptions { UseKebabCase = false }));

    [Fact]
    public void GetApiServiceRoute_default_prefix_and_postfix()
        => Assert.Equal("api/app-service/todo/[action]", ApiRouteBuilder.GetApiServiceRoute("TodoApplicationService"));

    [Fact]
    public void GetApiServiceRoute_honors_custom_prefix()
        => Assert.Equal("api/services/todo/[action]",
            ApiRouteBuilder.GetApiServiceRoute("TodoApplicationService", options: new RouteOptions { Prefix = "api/services" }));

    [Fact]
    public void GetApiServiceRoute_honors_custom_postfixes()
        => Assert.Equal("api/app-service/todo-manager/[action]",
            ApiRouteBuilder.GetApiServiceRoute("TodoManagerHandler",
                options: new RouteOptions { ControllerPostfixes = new[] { "Handler" } }));

    [Fact]
    public void GetApiServiceRouteFromInterface_strips_interface_prefix()
        => Assert.Equal("api/app-service/todo/get-todos",
            ApiRouteBuilder.GetApiServiceRouteFromInterface("ITodoAppService", "get-todos"));
}
