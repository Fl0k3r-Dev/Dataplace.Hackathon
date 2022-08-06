using C1.Win.C1TrueDBGrid;
using Dataplace.Core.Application.Services.Results;
using Dataplace.Core.Comunications;
using Dataplace.Core.Domain.Localization.Messages.Extensions;
using Dataplace.Core.Domain.Notifications;
using Dataplace.Core.Infra.CrossCutting.EventAggregator.Contracts;
using Dataplace.Core.win.Controls.List.Behaviors;
using Dataplace.Core.win.Controls.List.Behaviors.Contracts;
using Dataplace.Core.win.Controls.List.Configurations;
using Dataplace.Core.win.Views.Controllers;
using Dataplace.Core.win.Views.Providers;
using Dataplace.Imersao.Core.Application.Orcamentos.Commands;
using Dataplace.Imersao.Core.Application.Orcamentos.Queries;
using Dataplace.Imersao.Core.Application.Orcamentos.ViewModels;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using Dataplace.Imersao.Presentation.Common;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Messages;
using dpLibrary05;
using dpLibrary05.Infrastructure.Helpers;
using dpLibrary05.Infrastructure.Helpers.Permission;
using dpLibrary05.SymphonyInterface;
using MediatR;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Dataplace.Imersao.Presentation.Views.Orcamentos.Tools
{
    public partial class CancelarFehacrOrcamentosView : dpLibrary05.Infrastructure.UserControls.ucSymGen_ToolDialog
    {
        #region fields
        private DateTime _startDate;
        private DateTime _endDate;
        private const int _itemSeg = 467;
        private IListBehavior<OrcamentoViewModel, OrcamentoQuery> _orcamentoList;
        
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly RegisterViewController _c;

        private const string _Path = "\\Arquivos\\";

        private fSymGen_DLG_ImageList _ImageList;
        #endregion

        #region constructors
        public CancelarFehacrOrcamentosView(
            IServiceProvider serviceProvider, 
            IEventAggregator eventAggregator)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _eventAggregator = eventAggregator;

            _orcamentoList = new C1TrueDBGridListBehavior<OrcamentoViewModel, OrcamentoQuery>(gridOrcamento)
                .WithConfiguration(GetConfiguration());

            this.ToolConfiguration += CancelamentoOrcamentoView_ToolConfiguration;
            this.BeforeProcess += CancelamentoOrcamentoView_BeforeProcess;
            this.Process += CancelamentoOrcamentoView_Process;
            this.AfterProcess += CancelamentoOrcamentoView_AfterProcess;


            this.tsiMarcar.Click += TsiMarcarTodos_Click;
            this.tsiDesmarca.Click += TsiDesmarcarTodos_Click;
            this.tsiExcel.Click += TsiExportarGridParaExcel_Click;

            this.KeyDown += CancelamentoOrcamentoView_KeyDown;


            this.chkAberto.Click += chk_Click;
            this.chkFechado.Click += chk_Click;
            this.chkCancelado.Click += chk_Click;


            // pegar evento clique das opçoes
            this.optCancelar.Click += opt_Click;
            this.optFechar.Click += opt_Click;
            this.optReabrir.Click += opt_Click;


            _startDate = DateTime.Today.AddMonths(-1);
            _endDate = DateTime.Today;
            rangeDate.Date1.Value = _startDate;
            rangeDate.Date2.Value = _endDate;


            // pegar key down de um controle
            // dtpPrevisaoEntrega.KeyDown += Dtp_KeyDown;



            // rotina para validar status do controle
            //  desabilitar ou habilitar algun componente em tela
            //  deixar invisível ou algo assim
            VerificarStatusControles();

            //Componentes
            dpiNumOrcamento.FindMode = true;

            configureBtn();

            dpiVendedor.DP_InputType = dpLibrary05.Infrastructure.Controls.DPInput.InputTypeEnum.SearchValueInput;
            if (dpiVendedor.CurrentControl is dpLibrary05.Infrastructure.Controls.DPLookUpEdit l_Vendedor)
            {
                l_Vendedor.DP_ShowCaption = false;
            }
            dpiVendedor.SearchObject = GetSearchVendedor();

            if (rangeDate.Date1.Parent is TableLayoutPanel t)
            {
                t.Width = 300;
            }

            _orcamentoList.DataSourceChanged += _orcamentoList_DataSourceChanged;

        }
        #endregion

        #region tool events

        private TipoAcaoEnum _tipoAcao;
        private enum TipoAcaoEnum
        {
            CancelarOrcamento,
            FecharOrcamento,
            ReabrirOrcamento
        }
        private void CancelamentoOrcamentoView_ToolConfiguration(object sender, ToolConfigurationEventArgs e)
        {
            // definições iniciais do projeto
            // item seguraça
            // engine code
            this.Text = "Cancelar/Fechar orçamentos em aberto";
            e.SecurityIdList.Add(_itemSeg);
            e.CancelButtonVisisble = true;
        }
        private void CancelamentoOrcamentoView_BeforeProcess(object sender, BeforeProcessEventArgs e)
        {
            // defaul 
            _tipoAcao = TipoAcaoEnum.CancelarOrcamento;

            if (optCancelar.Checked)
                _tipoAcao = TipoAcaoEnum.CancelarOrcamento;

            if (optFechar.Checked)
                _tipoAcao = TipoAcaoEnum.FecharOrcamento;

            if (optReabrir.Checked)
                _tipoAcao = TipoAcaoEnum.ReabrirOrcamento;



            var permission = PermissionControl.Factory().ValidatePermission(_itemSeg, dpLibrary05.mGenerico.PermissionEnum.Execute);
            if (!permission.IsAuthorized())
            {
                e.Cancel = true;
                this.Message.Info(permission.BuildMessage());
                return;
            }

            var itensSelecionados = _orcamentoList.GetCheckedItems();
            if(itensSelecionados.Count() == 0)
            {
                e.Cancel = true;
                this.Message.Info(53727.ToMessage());
                return;
            }


            e.Parameter.Items.Add("acao", _tipoAcao);
            e.Parameter.Items.Add("itensSelecionados", itensSelecionados);
        }
        private async void CancelamentoOrcamentoView_Process(object sender, ProcessEventArgs e)
        {

            var acao = (TipoAcaoEnum)e.Parameter.Items.get_Item("acao").Value;
            var itensSelecionados = (IEnumerable<OrcamentoViewModel>)e.Parameter.Items.get_Item("itensSelecionados").Value;

            e.ProgressMinimum = 0;
            e.ProgressMaximum = itensSelecionados.Count();
            e.BeginProcess();

            // um a um
            foreach (var item in itensSelecionados)
            {

                switch (acao)
                {
                    case TipoAcaoEnum.CancelarOrcamento:
                        await CancelarOrcamento(item);
                        // registrar log na parte de detalhes
                        e.LogBuilder.Items.Add($"Orçamento {item.NumOrcamento} cancelado", dpLibrary05.Infrastructure.Helpers.LogBuilder.LogTypeEnum.Information);
                        break;
                    case TipoAcaoEnum.FecharOrcamento:
                        await FecharOrcamento(item);
                        // registrar log na parte de detalhes
                        e.LogBuilder.Items.Add($"Orçamento {item.NumOrcamento} fechado", dpLibrary05.Infrastructure.Helpers.LogBuilder.LogTypeEnum.Information);
                        break;
                    case TipoAcaoEnum.ReabrirOrcamento:
                        await ReabrirOrcamento(item);
                        // registrar log na parte de detalhes
                        e.LogBuilder.Items.Add($"Orçamento {item.NumOrcamento} reaberto", dpLibrary05.Infrastructure.Helpers.LogBuilder.LogTypeEnum.Information);
                        break;
                    default:
                        break;
                }


                
                // permitir cancelamento 
                if (e.CancellationRequested)
                   break;

                e.ProgressValue += 1;
            }

            e.EndProcess();
        }
        private void CancelamentoOrcamentoView_AfterProcess(object sender, AfterProcessEventArgs e)
        {
            // exemplo de message box no final do processo
            this.Message.Info("Sucesso no processamento!");

            //  desmarcar todos itens no final do processo
            _orcamentoList.ChangeCheckState(false);
        }

        // teclas de atalho
        private async void CancelamentoOrcamentoView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                await _orcamentoList.LoadAsync();
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.M)
            {
                _orcamentoList.ChangeCheckState(true);
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.D)
            {
                _orcamentoList.ChangeCheckState(false);
            }

        }

        #endregion

        #region list events

        // exemplos conf list
        //  configuration.AllowFilter();  >> permite filtro
        //  configuration.AllowSort(); >> habilita ordenação
        //  configuration.Ignore(x => x.CdVendedor); >> ignora 
        // 


        // adicionar botão (nesse caso seta azul)
        // configuration.Property(x => x.NumOrcamento)
        //    .HasButton(dpLibrary05.mGenerico.oImageList.imgList16.Images[dpLibrary05.mGenerico.oImageList.SETA_AZUL_PEQ], (sender, e) => {
        //        var item = (OrcamentoViewModel)sender;
        //        _eventAggregator.PublishEvent(new OrcamentoSetaAzulClick(item.NumOrcamento));
        //    });



        // exemplode de destaque das linhas
        //configuration.HasHighlight(x => {
        //    // destacando somente cor da fonte
        //    x.Add(orcamento => orcamento.StEntrega == "2", System.Drawing.Color.DarkOrange);

        //    // exemplo para destacar a cor da fonte e cor de fundo da linha
        //    x.Add(orcamento => orcamento.StEntrega == "2", new ViewModePropertyHighlightStyle()
        //        .WithBackColor(System.Drawing.Color.DarkOrange)
        //        .WithForeColor(System.Drawing.Color.White));
        //});


        // exemplo de tradução para valores na coluna
        //configuration.Property(x => x.StAlgumaCoisa)
        //   .HasCaption("St. validade")
        //   .HasValueItems(x =>
        //   {
        //       x.Add("0", "texto para equivalente ao valor 0");
        //       x.Add("1", "texto para equivalente ao valor 1");
        //       x.Add("2", "texto para equivalente ao valor 2");
        //   });

        private ViewModelListBuilder<OrcamentoViewModel> GetConfiguration()
        {
            var configuration = new ViewModelListBuilder<OrcamentoViewModel>();

         

            configuration.HasHighlight(x => {
                x.Add(orcamento => orcamento.Situacao == Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Cancelado.ToDataValue(), System.Drawing.Color.Red);
            });

            // define rotina para obter os filtros que vão ser aplicados na query
            configuration.WithQuery<OrcamentoQuery>(() => GetQuery());

            configuration.Ignore(x => x.CdEmpresa);
            configuration.Ignore(x => x.CdFilial);
            configuration.Ignore(x => x.SqTabela);
            configuration.Ignore(x => x.CdTabela);
            configuration.Ignore(x => x.CdVendedor);
            configuration.Ignore(x => x.DiasValidade);
            configuration.Ignore(x => x.DataValidade);
            configuration.Ignore(x => x.TotalItens);

            configuration.SetAllowFilter(true);
            configuration.SetAllowSort(true);

            configuration.Property(x => x.Situacao)
                  .HasMinWidth(100)
                  .HasCaption("Situação")
                  .HasValueItems(x =>
                  {
                      x.Add(OrcamentoStatusEnum.Aberto.ToDataValue(), 3469.ToMessage());
                      x.Add(OrcamentoStatusEnum.Fechado.ToDataValue(), 3846.ToMessage());
                      x.Add(OrcamentoStatusEnum.Cancelado.ToDataValue(), 3485.ToMessage());
                  });

            configuration.Property(x => x.CdCliente)
               .HasMinWidth(50)
               .HasCaption("Cliente");

            configuration.Property(x => x.DsCliente)
                .HasMinWidth(200)
                .HasCaption("Razão");

            configuration.Property(x => x.DtOrcamento)
                .HasMinWidth(80)
                .HasCaption("Data")
                .HasFormat("d");

            configuration.Property(x => x.VlTotal)
                .HasMinWidth(80)
                .HasCaption("Total")
                .HasFormat("n");


            configuration.Property(x => x.DtFechamento)
                .HasMinWidth(80)
                .HasCaption("Fechamento")
                .HasFormat("d");

            return configuration;
        }

        private OrcamentoQuery GetQuery()
        {
            var situacaoList = new List<Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum>();
            if (chkAberto.Checked)
                situacaoList.Add(Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Aberto);
            if (chkFechado.Checked)
                situacaoList.Add(Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Fechado);
            if (chkCancelado.Checked)
                situacaoList.Add(Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Cancelado);

            DateTime? dtInicio = null;
            DateTime? dtFim = null;

            if (rangeDate.Date1.Value is DateTime d && activeSearchData.Checked)
                dtInicio = d;

            if (rangeDate.Date2.Value is DateTime d2 && activeSearchData.Checked)
                dtFim = d2;

            var cpoNumOrcamento = dpiNumOrcamento.GetValue().ToString() ?? string.Empty;
            var cpoVendedor = dpiVendedor.GetValue().ToString() ?? string.Empty;
            var query = new OrcamentoQuery() {
                SituacaoList = situacaoList,
                DtInicio =  dtInicio,
                DtFim =  dtFim,
                NumOrcamento = cpoNumOrcamento,
                Vendedor = cpoVendedor
             };
            return query;
        }

        #endregion

        #region contol events

        private void TsiExportarGridParaExcel_Click(object sender, EventArgs e)
        {
            clsOffice.ExportTrueDbGridToExcel(gridOrcamento, xlsOption.xlsSaveAndOpen);
        }
        private void exportarParaPDFToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int codigoArquivoPDF = ExportToPDF();

                if (codigoArquivoPDF == 0)
                    MessageBox.Show("Não há registro(s) para exportar!");
                    return;
                
                MessageBox.Show($"Sucesso ao exportar para PDF! Arquivo exportado em: '{Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.ToString()) + _Path + codigoArquivoPDF}'");
                   
        }
        private void TsiDesmarcarTodos_Click(object sender, EventArgs e)
        {
            _orcamentoList.ChangeCheckState(false);
        }
        private void TsiMarcarTodos_Click(object sender, EventArgs e)
        {
            _orcamentoList.ChangeCheckState(true);
        }

        private async void BtnCarregar_Click(object sender, EventArgs e)
        {
            await _orcamentoList.LoadAsync();
        }
 
        private async void chk_Click(object sender, EventArgs e)
        {
            await _orcamentoList.LoadAsync();
        }
        private void opt_Click(object sender, EventArgs e)
        {
            VerificarStatusControles();
        }


        private void VerificarStatusControles() {

            // exemplo pra deixar componente intaivo dependendo de uma opão
            // dtpPrevisaoEntrega.Enabled = optAtribuirPevisaoEntrega.Checked;


        }
        #endregion

        #region processamentos
        private async Task CancelarOrcamento(OrcamentoViewModel item) 
        {

            using (var scope = dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.Factory())
            {

                var command = new CancelarOrcamentoCommand(item);
                var mediator = scope.Container.GetInstance<IMediatorHandler>();

                var notifications = scope.Container.GetInstance<INotificationHandler<DomainNotification>>();
                await mediator.SendCommand(command);

                item.Result = Result.ResultFactory.New(notifications.GetNotifications());
                if (item.Result.Success)
                {
                    item.Situacao = Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Cancelado.ToDataValue();
                }

            }

        }

        private async Task FecharOrcamento(OrcamentoViewModel item)
        {

            using (var scope = dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.Factory())
            {

                var command = new FecharOrcamentoCommand(item);
                var mediator = scope.Container.GetInstance<IMediatorHandler>();

                var notifications = scope.Container.GetInstance<INotificationHandler<DomainNotification>>();
                await mediator.SendCommand(command);

                item.Result = Result.ResultFactory.New(notifications.GetNotifications());
                if (item.Result.Success)
                {
                    item.Situacao = Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Fechado.ToDataValue();
                    item.DtFechamento = DateTime.Now.Date;
                }

            }

        }

        private async Task ReabrirOrcamento(OrcamentoViewModel item)
        {

            using (var scope = dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.Factory())
            {

                var command = new ReabrirOrcamentoCommand(item);
                var mediator = scope.Container.GetInstance<IMediatorHandler>();

                var notifications = scope.Container.GetInstance<INotificationHandler<DomainNotification>>();
                await mediator.SendCommand(command);

                item.Result = Result.ResultFactory.New(notifications.GetNotifications());
                if (item.Result.Success)
                {
                    item.Situacao = Core.Domain.Orcamentos.Enums.OrcamentoStatusEnum.Aberto.ToDataValue();
                    item.DtFechamento = null;
                }

            }

        }

        #endregion

        #region Another Methods

        private void configureBtn()
        {
            _ImageList = new fSymGen_DLG_ImageList();
            btnCarregar.Image = _ImageList.imgList32.Images[_ImageList.SEARCH_NEW_28];
        }

       private int generateCodePDF() 
       {
            Random rnd = new Random();
            return rnd.Next();
       }

        private ISymInterfaceSearch _searchVendedor;

        private ISymInterfaceSearch GetSearchVendedor()
        {
            if (_searchVendedor != null)
                return _searchVendedor;

            var prmVendendor = new dpLibrary05.Infrastructure.Helpers.clsSymSearch.SearchArgs()
            {
                Fields = new List<dpLibrary05.SymphonyInterface.clsSymInterfaceSearchField>()
                {
                    new dpLibrary05.SymphonyInterface.clsSymInterfaceSearchField(){
                        SearchIndex = 2,
                        VisibleEdit = false
                    },
                    new dpLibrary05.SymphonyInterface.clsSymInterfaceSearchField(){
                        SearchIndex = 3,
                        VisibleEdit = false
                    },
                    new dpLibrary05.SymphonyInterface.clsSymInterfaceSearchField(){
                        SearchIndex = 4,
                        VisibleEdit = false
                    }
                }
            };
            _searchVendedor = dpLibrary05.Infrastructure.Helpers.clsSymSearch.find_vendedor(prmVendendor);

            return _searchVendedor;

        }

        private ISymInterfaceSearch _clienteSearchObject;
        private ISymInterfaceSearch GetClienteSearchObject()
        {

            if (_clienteSearchObject == null)
                _clienteSearchObject = PedidoSearch.find_orcamento_cliente();

            _clienteSearchObject.SetaAzul = false;

            _clienteSearchObject.BeforeSearch += (object sender, BeforeSearchEventArgs e) =>
            {
                e.SearchObject.Filter = "1=1";
            };

            _clienteSearchObject.AfterSearch += (object sender, AfterSearchEventArgs e) =>
            {
                var _interfaceMode = _c.GetInterfaceMode();

                if (_interfaceMode != InterfaceModeEnum.Inserting)
                    return;

                //if (e.result)
                //    ClienteSelecionado(e.value.ToString(), true);
            };

            return _clienteSearchObject;
        }


        private void enviarParaWhatsAppToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int codigoArquivoExportado = ExportToPDF();

            var caminhoArquivo = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.ToString()) + _Path + codigoArquivoExportado + ".pdf";

            if (File.Exists(caminhoArquivo))
            {
                byte[] objByte = File.ReadAllBytes(caminhoArquivo);
                string arquivo = Convert.ToBase64String(objByte);
                File.WriteAllText(caminhoArquivo, arquivo);

                var client = new RestClient("https://app.whatsgw.com.br/api/WhatsGw/Send");
                var request = new RestRequest("911b096c-9619-44da-9f4e-e42172f6ce7c", Method.Post);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("apikey", "911b096c-9619-44da-9f4e-e42172f6ce7c");
                request.AddParameter("phone_number", "5514996445166");
                request.AddParameter("contact_phone_number", "5514981196851");
                request.AddParameter("message_custom_id", "yowsoftwareid");
                request.AddParameter("message_type", "image");
                request.AddParameter("message_caption", "Notificando...");
                request.AddParameter("message_body", $"{arquivo}.pdf");
                request.AddParameter("message_body_mimetype", "application/pdf");
                request.AddParameter("message_body_filename", $"{codigoArquivoExportado}.pdf");
                request.AddParameter("download", "1");
                RestResponse response = client.Post(request);
                var teste = response.Content;
                if (response.StatusCode == HttpStatusCode.OK)
                    MessageBox.Show("Sucesso na transmissão!");
                gridOrcamento.Refresh();
            }
            else
            {
                MessageBox.Show("Falha na transmissão!");
            }
    }

        private void _orcamentoList_DataSourceChanged(object sender, Dataplace.Core.win.Controls.List.Delegates.DataSourceChangedEventArgs<OrcamentoViewModel> e)
        {
            gridOrcamento.Splits[0].DisplayColumns["DtFechamento"].Style.HorizontalAlignment = C1.Win.C1TrueDBGrid.AlignHorzEnum.Far;
        }

        private int ExportToPDF()
        {
            try
            {
                if (gridOrcamento.RowCount > 0)
                {

                    int codigoGerado = generateCodePDF();
                    gridOrcamento.ExportToPDF(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.ToString()) + _Path + $"{codigoGerado}.pdf");
                    return codigoGerado;
                }
                MessageBox.Show("Não há registros!");
                return 0;
            }
            catch (Exception ex)
            {
                SymException.ShowMessage(ex, System.Reflection.MethodBase.GetCurrentMethod());
                return 0;
            }
        }
        #endregion

    }
}
