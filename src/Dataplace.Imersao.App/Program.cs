using Dataplace.Core;
using Dataplace.Core.Shared.Catalog.Produto.Queries;
using Dataplace.Core.Shared.Catalog.Produto.ViewModels;
using Dataplace.Core.win.Views;
using Dataplace.Core.win.Views.Extensions;
using Dataplace.Imersao.Core.Application.Behaviors;
using Dataplace.Imersao.Core.Application.Produtos.Queries;
using Dataplace.Imersao.Core.Domain.Orcamentos.Repositories;
using Dataplace.Imersao.Core.Domain.Produtos.Repositories;
using Dataplace.Imersao.Core.Domain.Services;
using Dataplace.Imersao.Core.Infra.Data.Repositories;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Tools;
using Dataplace.Imersao.Presentation.Views.Providers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Dataplace.Imersao.App
{
    internal static class Program
    {
         
        [STAThread]
        static void Main(string[] args)
        {

            var accountSid = "MGf40834462a3ace5fa86c02b4ed69c419";
            var authToken = "[AuthToken]";

            TwilioClient.Init(accountSid, authToken);

            var executingAssembly = new Assembly[]
                { Assembly.GetExecutingAssembly(), typeof(Dataplace.Imersao.Core.Boot).Assembly};

            var builder = Dataplace.Core.DataplaceApplication.CreateBuilder(args)
                .UseAppName("SALESAPP")
                .UseLayout(AppLayoutEnum.Basic)
                .UseMediatR(executingAssembly)
                .OnLoadApp(x=> {
  
                })
                .OnCloseApp(x => { 
                
                });
            ConfigureServices(builder.Services);
            var app = builder.Build();
            app.Run<MainView>();
        }


        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MainView>();
            services.AddSingleton<CancelarFehacrOrcamentosView>();

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

            //services.AddScoped<IPipelineBehavior<ExcluirOrcamentoCommand, bool>, Dataplace.Core.Comunications.Behaviors.DeadlockRetryBehavior<ExcluirOrcamentoCommand, bool>>();

            // presentation - view provider
            services.AddRegisterViewProvider<InterfaceView, OrcamentoViewProvider>();
            services.AddRegisterViewProvider<InterfaceView, OrcamentoItemViewProvider>();


            // product query
            services.AddScoped<IRequestHandler<ProdutoSearchQuery, IEnumerable<ProdutoSearchModel>>, ProdutoSerachQueryHandler>();
            services.AddScoped<IRequestHandler<ProdutoVariacaoSearchQuery, IEnumerable<ProdutoSearchModel>>, ProdutoSerachQueryHandler>();
            services.AddScoped<IRequestHandler<ProdutoGradeSearchQuery, IEnumerable<ProdutoSearchModel>>, ProdutoSerachQueryHandler>();


            // domain - services
            services.AddScoped<IOrcamentoService, OrcamentoService>();

            // domain - repositories
            services.AddScoped<IProdutoRepository, ProdutoRepository>();
            services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();


        }

    }

}
