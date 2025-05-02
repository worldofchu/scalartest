using Entity;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using System.Reflection;
using System.Xml.XPath;
using Utility;

namespace WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            //Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            //添加 AutoMapper 的配置
            //使用AddAutoMapper()方法可以将AutoMapper所需的服务添加到该集合中，以便在应用程序的其他部分中使用。
            //该方法需要传入一个Assembly数组，以告诉AutoMapper要扫描哪些程序集来查找映射配置(在当前作用域的所有程序集里面扫描AutoMapper的配置文件)。
            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            builder.Services.AddControllers().AddNewtonsoftJson(options =>
            {
                //修改属性名称的序列化方式[前端想要使用与后端模型本身命名格式输出]
                options.SerializerSettings.ContractResolver = null;
                //日期类型默认格式化处理 
                //options.SerializerSettings.Converters.Add(new IsoDateTimeConverter() { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" });
            });

            // 添加Swagger服务
            //builder.Services.AddSwaggerGen(options =>
            //{
            //    options.SwaggerDoc("v1", new OpenApiInfo
            //    {
            //        Title = "EasySQLite API",
            //        Version = "V1",
            //        Description = ".NET 8操作SQLite入门到实战",
            //        Contact = new OpenApiContact
            //        {
            //            Name = "GitHub源码地址",
            //            Url = new Uri("https://github.com/YSGStudyHards/EasySQLite")
            //        }
            //    });

            //    // 获取xml文件名
            //    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            //    // 获取xml文件路径
            //    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            //    // 添加控制器层注释，true表示显示控制器注释
            //    options.IncludeXmlComments(xmlPath, true);
            //    // 对action的名称进行排序，如果有多个，就可以看见效果了
            //    options.OrderActionsBy(o => o.RelativePath);
            //});
            
            // 获取 XML 文件路径
            // 获取 XML 文件路径
            // 加载 XML 注释

            builder.Services.AddOpenApiDocument(document =>
{
    document.Title = "My .NET 8 API";
    document.Version = "v1";
    document.Description = "基于 NSwag + Scalar 的 API 文档";






    // 启用 XML 注释（需在项目属性中生成 XML 文档文件）
    // document.xml = true;
    // document.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "YourProject.xml"));
});

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            XmlCommentsHelper.LoadXmlComments(xmlPath);

            // 注册 API 描述服务
            builder.Services.AddSingleton<IApiDescriptionGroupCollectionProvider, ApiDescriptionGroupCollectionProvider>();
            // 添加OpenApi服务，这是Scalar所需的
            builder.Services.AddOpenApi(options =>
            {
                // 获取xml文件名
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                // 获取xml文件路径
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new()
                    {
                        Title = "EasySQLite API",
                        Version = "V1",
                        Description = ".NET 8操作SQLite入门到实战"
                    };
                    //
                    // 获取 API 描述信息
                    //
                    // 获取所有 API 描述
                    var apiDescriptionProvider = builder.Services.BuildServiceProvider()
                        .GetRequiredService<IApiDescriptionGroupCollectionProvider>();

                    foreach (var group in apiDescriptionProvider.ApiDescriptionGroups.Items)
                    {
                        foreach (var apiDescription in group.Items)
                        {
                            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
                                continue;

                            var methodInfo = actionDescriptor.MethodInfo;
                            var httpMethod = apiDescription.HttpMethod;
                            var relativePath = $"/{apiDescription.RelativePath}";

                            // 匹配 OpenAPI 路径和操作
                            if (!document.Paths.TryGetValue(relativePath, out var pathItem))
                                continue;

                            OperationType? operationType;
                            switch (httpMethod.ToLower())
                            {
                                case "get":
                                    operationType = OperationType.Get;
                                    break;
                                case "post":
                                    operationType = OperationType.Post;
                                    break;
                                case "put":
                                    operationType = OperationType.Put;
                                    break;
                                case "delete":
                                    operationType = OperationType.Delete;
                                    break;
                                default:
                                    operationType = null;
                                    break;
                            }

                            if (operationType == null || !pathItem.Operations.TryGetValue(operationType.Value, out var operation))
                                continue;

                            // 注入方法摘要
                            var methodSummary = XmlCommentsHelper.GetMethodSummary(methodInfo);
                            if (!string.IsNullOrEmpty(methodSummary))
                            {
                                operation.Summary ??= methodSummary;
                            }

                            // 注入参数注释

                            foreach (var parameter in operation.Parameters??new List<OpenApiParameter>())
                            {
                                var paramComment = XmlCommentsHelper.GetParameterComment(methodInfo, parameter.Name);
                                if (!string.IsNullOrEmpty(paramComment))
                                {
                                    parameter.Description ??= paramComment;
                                }
                            }

                            // 注入返回值注释
                            var returnsComment = XmlCommentsHelper.GetReturnsComment(methodInfo);
                            if (!string.IsNullOrEmpty(returnsComment))
                            {
                                operation.Responses.TryGetValue("200", out var response);
                                response ??= new OpenApiResponse();
                                response.Description ??= returnsComment;
                            }
                        }
                    }

                    // 注入模型属性注释
                    foreach (var schema in (document.Components?.Schemas)??new Dictionary<string, OpenApiSchema>())
                    {
                        var type = Type.GetType(schema.Key);
                        if (type == null) continue;

                        foreach (var property in schema.Value.Properties)
                        {
                            var propSummary = XmlCommentsHelper.GetPropertySummary(type, property.Key);
                            if (!string.IsNullOrEmpty(propSummary))
                            {
                                property.Value.Description ??= propSummary;
                            }
                        }
                    }

                    //



                    //
                    OpenApiServer oas = document.Servers.FirstOrDefault();
                    if (oas != null)
                    {
                        oas.Url= System.IO.Path.Combine(oas.Url, "/abc");
                    }
    
                    return Task.CompletedTask;
                });
                options.ShouldInclude=((s) => true);
                //
                
                //
            });
          
 
            var PolicyCorsName = "EasySQLitePolicy";

            builder.Services.AddCors(option =>
            {
                option.AddPolicy(PolicyCorsName, builder =>
                {
                    builder.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
                });
            });

            
            builder.Services.AddScoped<SQLiteAsyncHelper<SchoolClass>>();
            builder.Services.AddScoped<SQLiteAsyncHelper<Student>>();

            var app = builder.Build();
            app.UsePathBase("/abc");
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
               app.UseOpenApi(c=>
               {
                   c.PostProcess = (a, b) =>
                   {

                       a.BasePath = "/abc";
                       
                 
                   };
                   
                   //c.Path = "/abc" + c.Path;
               }); // 生成 OpenAPI 文档
                app.UseSwaggerUi(c=>
                {
                    var sdf = c.ServerUrl;

                    c.DocumentPath = "/abc" + c.DocumentPath;///swagger/{documentName}/swagger.json
                });
               

              app.MapScalarApiReference("/abc/scalar",options =>
              {
                  options.HideClientButton = true;
                  options.Layout = ScalarLayout.Classic;
                 // options.OpenApiRoutePattern = "/abc" + options.OpenApiRoutePattern;
                   
              });
                app.MapOpenApi(); 





            }

            app.UseHttpsRedirection();

            app.UseCors(PolicyCorsName);

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
