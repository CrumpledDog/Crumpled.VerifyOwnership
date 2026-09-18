using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Crumpled.VerifyOwnership.Composers
{
    // Umbraco 17 (Swashbuckle) OpenAPI document registration. See OpenApiRegistration.Umbraco18.cs for the
    // Umbraco 18 (native OpenApi) equivalent - the .csproj conditionally compiles only one of the two based
    // on $(UmbracoTargetMajor) from the repo-root Directory.Build.props.
    public class CrumpledVerifyOwnershipOpenApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

            builder.Services.Configure<SwaggerGenOptions>(opt =>
            {
                opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
                {
                    Title = "Crumpled Verify Ownership Backoffice API",
                    Version = "1.0",
                });

                opt.OperationFilter<CrumpledVerifyOwnershipOperationSecurityFilter>();
            });
        }

        public class CrumpledVerifyOwnershipOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
        {
            protected override string ApiName => Constants.ApiName;
        }

        public class CustomOperationHandler : OperationIdHandler
        {
            public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
            {
            }

            protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            {
                return controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("Crumpled.VerifyOwnership.Controllers", comparisonType: StringComparison.InvariantCultureIgnoreCase) is true;
            }

            public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
        }
    }
}
