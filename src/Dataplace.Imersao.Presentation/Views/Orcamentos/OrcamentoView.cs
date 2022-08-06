using Dataplace.Core.Comunications;
using Dataplace.Core.Domain.Localization.Messages.Extensions;
using Dataplace.Core.Infra.CrossCutting.EventAggregator.Contracts;
using Dataplace.Core.win.Controls.Binding;
using Dataplace.Core.win.Views.Contracts;
using Dataplace.Core.win.Views.Controllers;
using Dataplace.Core.win.Views.Extensions;
using Dataplace.Core.win.Views.Managers;
using Dataplace.Core.win.Views.Providers;
using Dataplace.Imersao.Core.Application.Orcamentos.Queries;
using Dataplace.Imersao.Core.Application.Orcamentos.ViewModels;
using Dataplace.Imersao.Core.Domain.Orcamentos.Enums;
using Dataplace.Imersao.Presentation.Common;
using Dataplace.Imersao.Presentation.Views.Orcamentos.Messages;
using Dataplace.Imersao.Presentation.Views.Providers;
using dpLibrary05.Infrastructure.Controls;
using dpLibrary05.Infrastructure.Helpers;
using dpLibrary05.SymphonyInterface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace Dataplace.Imersao.Presentation.Views.Orcamentos
{
    public partial class OrcamentoView : UserControl, 
        IRegisterView<OrcamentoViewModel>,
        ISubscriber<OrcamentoItemAtualizadoMessage>
    {
        #region fields
        private OrcamentoViewModel _orcamento;
        private readonly RegisterViewController _c;
        private readonly PartialViewsManager _pvm;
        private readonly DataBindingViewModel<OrcamentoViewModel> _b;

        #endregion

        #region contructor
        public OrcamentoView(RegisterViewController c)
        {
           
            InitializeComponent();
            _c = c;
            var ea = dpLibrary05.BootStrapper.Container.GetInstance<IEventAggregator>();
            ea.SubscribeMe(this);

            // events
            _c.AfterAdd += async (object sender, Dataplace.Core.win.Controls.Delegates.AfterAddEventArgs e) =>
            {
                if (await _c.Update())
                    ea.PublishEvent(new OrcamentoAdicionadoMessage());
            };


            dpiNumOrcamento.FindMode = true;

            // componentes
            dpiCliente.DP_InputType = dpLibrary05.Infrastructure.Controls.DPInput.InputTypeEnum.SearchValueInput;
            if (dpiCliente.CurrentControl is dpLibrary05.Infrastructure.Controls.DPLookUpEdit l)
            {
                l.DP_ShowCaption = true;
            }
            dpiCliente.SearchObject = GetClienteSearchObject();


            dpiSituacao.IsReadonly = true;
            if (dpiSituacao.CurrentControl is DPComboBox c_lblStPedido)
            {
                c_lblStPedido.DataSource = new List<clsSymValueItem>() {
                        new clsSymValueItem(OrcamentoStatusEnum.Aberto.ToDataValue(), 3469.ToMessage()),//Aberto
                        new clsSymValueItem(OrcamentoStatusEnum.Fechado.ToDataValue(), 3846.ToMessage()),//Bloqueado
                        new clsSymValueItem(OrcamentoStatusEnum.Cancelado.ToDataValue(), 3485.ToMessage()),//Cancelado
                };
            }


            dpiVendedor.DP_InputType = dpLibrary05.Infrastructure.Controls.DPInput.InputTypeEnum.SearchValueInput;
            if (dpiVendedor.CurrentControl is dpLibrary05.Infrastructure.Controls.DPLookUpEdit l_Vendedor)
            {
                l_Vendedor.DP_ShowCaption = false;
            }
            dpiVendedor.SearchObject = GetSearchVendedor();


            dpiValidadeDias.DP_Maximum = 100;
            dpiValidadeDias.DP_Minimum = 0;



            _b = new DataBindingViewModel<OrcamentoViewModel>(this)
                .Map(x => x.NumOrcamento, new DataBindingControl(dpiNumOrcamento))
                .Map(x => x.CdCliente, new DataBindingControl(dpiCliente,
                    (x) => _c.GetInterfaceMode() == InterfaceModeEnum.Inserting || _c.GetInterfaceMode() == InterfaceModeEnum.Searching))
                .Map(x => x.DtOrcamento, new DataBindingControl(dpiData, (x) => false))
                .Map(x => x.DtFechamento, new DataBindingControl(dpiFechamento, (x) => false))
                .Map(x => x.Situacao, new DataBindingControl(dpiSituacao, (x) => false))
                .Map(x => x.DiasValidade, new DataBindingControl(dpiValidadeDias,
                    (x) => _c.GetInterfaceMode() == InterfaceModeEnum.Inserting || _c.GetInterfaceMode() == InterfaceModeEnum.Editing || (_c.GetInterfaceMode() == InterfaceModeEnum.StandBy && _c.GetSelectedMode() == SelectedModeEnum.One)))
                .Map(x => x.DataValidade, new DataBindingControl(dpiValidadeData, (x) => false))
                .Map(x => x.CdVendedor, new DataBindingControl(dpiVendedor,
                    (x) => _c.GetInterfaceMode() == InterfaceModeEnum.Inserting || _c.GetInterfaceMode() == InterfaceModeEnum.Editing || (_c.GetInterfaceMode() == InterfaceModeEnum.StandBy && _c.GetSelectedMode() == SelectedModeEnum.One)))
                .Map(x => x.VlTotal, new DataBindingControl(dpiTotal, (x) => false));
            _c.AddBindings(_b);


            var _s = new SearchBindingManager(_c, GetOrcamentoSearchObject(), true)
                .Map(new SearchMapperInput(dpiNumOrcamento, 0, true, true))
                .Map(new SearchMapperLookUp((DPLookUpEdit)dpiCliente.CurrentControl, new List<SearchMapperLookUpItem>() {
                                            new SearchMapperLookUpItem(0, 1, true, false),
                                            new SearchMapperLookUpItem(1, 3, true, false)
            }));


            _pvm = new PartialViewsManager(tabMain);
            _pvm.RegisterInterfaceView<OrcamentoItemViewProvider>(tpItens, _c);
            //_pvm.RegisterInterfaceView<OrcamentoItemViewProvider>(tpFinanceiro, _c);
            //_pvm.RegisterView(tpAdicionais, () => new OrcamentoAdicionaisView(_c));
            _c.AddPartialViews(_pvm);

        }


     
        #endregion

        #region properties
        public RegisterViewController Controller => throw new System.NotImplementedException();

        public UserControl View => this;

        #endregion

        #region methods
        public void Init()
        {
            //_pvm.OpenInititalView();
        }
        public void Configure()
        {

        }

        public void ClearValue()
        {
            _orcamento = null;
        }


        public void GetValue(OrcamentoViewModel viewModel)
        {

        }


        public void SetValue(OrcamentoViewModel viewModel)
        {
            _orcamento = viewModel;
        }

        #endregion

        #region search objects
        private ISymInterfaceSearch _orcamentoSearchObject;
        private ISymInterfaceSearch GetOrcamentoSearchObject()
        {

            if (_orcamentoSearchObject == null)
                _orcamentoSearchObject = PedidoSearch.find_orcamento();

            _orcamentoSearchObject.BeforeSearch += (object sender, BeforeSearchEventArgs e) =>
            {
                e.SearchObject.Filter = "1=1";
            };

            _orcamentoSearchObject.AfterSearch += async (object sender, AfterSearchEventArgs e) =>
            {
                if (e.result)
                {
                    if (e.item is DataRow r)
                    {
                        if (r["NumOrcamento"] is int n)
                        {
                            await _c.Load(new OrcamentoViewModel() { NumOrcamento = n });
                        }

                    }
                }
            };

            return _orcamentoSearchObject;

        }

        private ISymInterfaceSearch _clienteSearchObject;
        private ISymInterfaceSearch GetClienteSearchObject()
        {

            if (_clienteSearchObject == null)
                _clienteSearchObject = PedidoSearch.find_orcamento_cliente();

            _clienteSearchObject.SetaAzul = true;
            _clienteSearchObject.SetaAzulClick += (ISymInterfaceSearch sender, EventArgs e) =>
                dpLibrary05.Infrastructure.Helpers.SetaAzulCall.SetaAzulConfigurationRepository.ClienteFornecedor(dpiCliente.GetValue().ToString(), "C").Execute();

            _clienteSearchObject.BeforeSearch += (object sender, BeforeSearchEventArgs e) =>
            {
                e.SearchObject.Filter = "1=1";

                //dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.WithScope((IDdEmpresa ddEmpresa) =>
                //{
                //    if (!ddEmpresa.StVdaCliInativo)
                //        e.SearchObject.Filter += "AND cliente.stativocf = 1";
                //});

                //dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.WithScope((IPrmVDA prmVDA, IPedidoRepository pedidoRepository, IVendedorService vendedorService) =>
                //{
                //    if (!prmVDA.StRestringeVisaoVendedor) return;

                //    var vendedoLogado = pedidoRepository.ObterVendedorPorUsuario(_value.UsuarioAlterandoPedido, _value.CdEmpresa, _value.CdFilial);
                //    if (vendedoLogado == null) return;

                //    var vendedoresPermitidos = vendedorService.ObterListaVendedoresDependentes(vendedoLogado);

                //    e.SearchObject.Filter += $"AND cliente.cdvendedor IN ({ string.Join(",", vendedoresPermitidos.ToList().Select(x => $"'{x.CdVendedor.Trim()}'")) })";
                //});
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


        #endregion

        #region message 
        public async void OnEventHandler(OrcamentoItemAtualizadoMessage e)
        {
           
            using  (var scope =  dpLibrary05.Infrastructure.ServiceLocator.ServiceLocatorScoped.Factory())
            {
                var m = scope.Container.GetInstance<IMediatorHandler>();

                var r = await m.Query(new OrcamentoRefreshQuery() { NumOrcamento = _c.GetParameter<OrcamentoViewModel>().NumOrcamento });

                if (r != null)
                    _b.SetValue(r);
            }
        }
        #endregion
    }
}
