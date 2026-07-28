using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Omni.AutoApi.AspNetCore;
using Xunit;

namespace Omni.AutoApi.Tests;

public class ConventionCollisionTests
{
    public class CollidingService : IRemoteService
    {
        public Task<int> GetThingAsync(int id) => Task.FromResult(id);
        public Task<int> GetThingAsync(string slug) => Task.FromResult(0);
    }

    public class HealthyService : IRemoteService
    {
        public Task<int> GetThingAsync(int id) => Task.FromResult(id);
        public Task CreateThingAsync(int id) => Task.CompletedTask;
    }

    [Fact]
    public void Overloaded_methods_fail_at_startup_with_clear_message()
    {
        var controller = BuildControllerModel(typeof(CollidingService));

        var ex = Assert.Throws<InvalidOperationException>(
            () => new AutoApiControllerConvention().Apply(controller));

        Assert.Contains("rota duplicada", ex.Message);
        Assert.Contains("GetThingAsync", ex.Message);
        Assert.Contains("[HttpGet", ex.Message);   // sugestão de desambiguação
    }

    [Fact]
    public void Distinct_actions_do_not_collide()
    {
        var controller = BuildControllerModel(typeof(HealthyService));

        new AutoApiControllerConvention().Apply(controller);   // não lança

        Assert.All(controller.Actions, a => Assert.Single(a.Selectors));
    }

    private static ControllerModel BuildControllerModel(Type type)
    {
        var typeInfo = type.GetTypeInfo();
        var controller = new ControllerModel(typeInfo, typeInfo.GetCustomAttributes(inherit: true))
        {
            ControllerName = type.Name
        };

        foreach (var method in typeInfo.DeclaredMethods.Where(m => m.IsPublic && !m.IsSpecialName))
        {
            controller.Actions.Add(new ActionModel(method, method.GetCustomAttributes(inherit: true))
            {
                ActionName = method.Name,
                Controller = controller
            });
        }

        return controller;
    }
}
