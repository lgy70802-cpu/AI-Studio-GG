module.exports = {
  PUUID: 'asharestocks',
  Version: '0.10.0',
  APIVersion: '1.0',
  CategoryIcon: 'images/icon.svg',
  i18n: { en: { Name: 'A Share Stocks', Description: 'A-share watchlist' }, zh_CN: { Name: 'A股行情', Description: '自选股行情看板' } },
  Actions: [
    {
      UUID: 'action1',
      state: 0,
      Icon: 'images/icon.svg',
      i18n: { en: { Name: 'Watchlist', Tooltip: 'A-share watchlist' }, zh_CN: { Name: '自选行情', Tooltip: 'A股自选行情与分时' } },
      Settings: {
        ActionGeometry: { width: 1920, height: 462 },
        symbols: 'sh600519,sz300750,sh600036,sh688981',
        interval: 10,
        pageSeconds: 12,
        greenUp: false,
        mode: 'list',
        focus: '',
        fontScale: 1,
        sort: 'manual',
        alertEnabled: false
      },
      States: [{ FontSize: '10', TitleAlignment: 'bottom' }],
      UserTitleEnabled: false,
      SupportedInMultiActions: false,
      Controllers: ['SecondaryScreen']
    }
  ]
};
