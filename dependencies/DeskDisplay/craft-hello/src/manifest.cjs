// ── Plugin & action declaration (source of truth) ──────────────────────────
// `script/autofile.cjs` reads this file to generate dist/manifest.json and every
// dist/<lang>.json. You edit THIS file, never the generated manifest.json.
//
// Rename rules (Craft):
//   • Plugin.UUID  = the plugin short name. Final plugin id becomes
//                    `com.mirabox.streamdock.<Plugin.UUID>`, installed as the
//                    folder `com.mirabox.streamdock.<Plugin.UUID>.mPanelPlugin`.
//   • Action UUID  = the action short name. Final action id becomes
//                    `com.mirabox.streamdock.<Plugin.UUID>.<UUID>`.
//   • Each action's `UUID` MUST match its file names:
//       src/plugin/actions/<UUID>.ts   (draw logic)
//       src/pages/actions/<UUID>.vue   (Property Inspector)
//   • Every action's Settings MUST contain `ActionGeometry { width, height }` —
//     the default canvas size Craft asks you to draw at.

const Plugin = {
  UUID: 'deskdisplayhello',
  version: '1.0.0',
  APIVersion: '1.0',
  Icon: 'images/icon.svg',
  i18n: {
    en:    { Name: 'D92 Art Clocks',  Description: 'Animated art clocks for Craft secondary displays' },
    zh_CN: { Name: 'D92 艺术时钟合集',  Description: '适用于 Craft 副屏的动态艺术时钟合集' },
    zh_HK: { Name: 'D92 藝術時鐘合集',  Description: '適用於 Craft 副屏的動態藝術時鐘合集' },
    ja:    { Name: 'D92 Art Clocks',  Description: 'Craft セカンダリーディスプレイ用アートクロック' },
    ko:    { Name: 'D92 Art Clocks',  Description: 'Craft 보조 화면용 아트 시계 모음' },
    fr:    { Name: 'D92 Art Clocks',  Description: 'Horloges artistiques pour écrans secondaires Craft' },
    de:    { Name: 'D92 Art Clocks',  Description: 'Künstlerische Uhren für Craft-Zweitdisplays' },
    es:    { Name: 'D92 Art Clocks',  Description: 'Relojes artísticos para pantallas secundarias Craft' },
    it:    { Name: 'D92 Art Clocks',  Description: 'Orologi artistici per display secondari Craft' },
    ru:    { Name: 'D92 Art Clocks',  Description: 'Арт-часы для дополнительных дисплеев Craft' },
    pl:    { Name: 'D92 Art Clocks',  Description: 'Zegary artystyczne do ekranów dodatkowych Craft' }
  }
};

const Actions = [
  {
    UUID: 'action1',
    Icon: 'images/icon.svg',
    i18n: {
      en:    { Name: 'Hello D92 Clock', Tooltip: 'Show a text card on the secondary screen' },
      zh_CN: { Name: '你好，D92', Tooltip: '在副屏上显示一张文字卡片' },
      zh_HK: { Name: '你好，D92', Tooltip: '在副屏上顯示一張文字卡片' },
      ja:    { Name: 'Hello D92 Clock', Tooltip: 'セカンダリスクリーンにテキストカードを表示' },
      ko:    { Name: 'Hello D92 Clock', Tooltip: '보조 화면에 텍스트 카드 표시' },
      fr:    { Name: 'Hello D92 Clock', Tooltip: 'Afficher une carte de texte sur l’écran secondaire' },
      de:    { Name: 'Hello D92 Clock', Tooltip: 'Eine Textkarte auf dem Zweitbildschirm anzeigen' },
      es:    { Name: 'Hello D92 Clock', Tooltip: 'Mostrar una tarjeta de texto en la pantalla secundaria' },
      it:    { Name: 'Hello D92 Clock', Tooltip: 'Mostra una scheda di testo sullo schermo secondario' },
      ru:    { Name: 'Hello D92 Clock', Tooltip: 'Показать текстовую карточку на вторичном экране' },
      pl:    { Name: 'Hello D92 Clock', Tooltip: 'Pokaż kartę tekstową na ekranie pomocniczym' }
    },
    state: 0,
    States: [{ FontSize: '10', TitleAlignment: 'bottom' }],
    Settings: {
      // REQUIRED on every Craft action: the default canvas size to draw at.
      ActionGeometry: { width: 600, height: 240 },
      // Example user settings — edit/extend freely (these are NOT special):
      text: '你好，D92',
      bgColor: '#101820',
      textColor: '#72e4cc',
      showSeconds: true, showDate: true, showWeekday: true, hour12: false, transparent: false
    },
    UserTitleEnabled: false,
    SupportedInMultiActions: false,
    Controllers: ['SecondaryScreen']
  }
];

Actions.push({UUID:'flip',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'机械翻页时钟':'Mechanical Flip Clock',Tooltip:lang.startsWith('zh')?'米白翻牌与实时动画':'Animated flip clock'}])),Settings:{ActionGeometry:{width:1080,height:410},animate:true,duration:400,transport:'compatible',demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'stringclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'弦线数字时钟':'String Art Clock',Tooltip:lang.startsWith('zh')?'几何骨架与弦线数字':'Geometric string art digits'}])),Settings:{ActionGeometry:{width:1000,height:320},showSeconds:false,threads:true,shadow:true,blink:true,bgColor:'#9d85a5',lineColor:'#121014'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'ribbonclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'流线数字时钟':'Ribbon Clock',Tooltip:lang.startsWith('zh')?'多重平行曲线数字':'Parallel ribbon digits'}])),Settings:{ActionGeometry:{width:1000,height:340},showSeconds:false,blink:true,bgColor:'#090909',lineColor:'#ff1735'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'foldclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'折光数字时钟':'Folded Light Clock',Tooltip:lang.startsWith('zh')?'弧面折带数字':'Folded gradient digits'}])),Settings:{ActionGeometry:{width:1000,height:340},showSeconds:false,blink:true,bgColor:'#000000',lineColor:'#ffffff'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'auroraclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'极光粒子时钟':'Aurora Particle Clock',Tooltip:'Particle ribbon clock'}])),Settings:{ActionGeometry:{width:1000,height:420},animate:true,duration:800,breath:true,demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'stardustclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'星尘数字时钟':'Stardust Clock',Tooltip:'Silver-blue stardust clock'}])),Settings:{ActionGeometry:{width:1000,height:420},animate:true,duration:800,drift:true,demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'referenceclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'原图颗粒时钟':'Reference Particle Clock',Tooltip:'Reference artwork clock with reconstructed zero'}])),Settings:{ActionGeometry:{width:1100,height:420},animate:true,duration:400,drift:true,demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'porcelainclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'白瓷指针时钟':'Porcelain Clock',Tooltip:'Soft white analog clock'}])),Settings:{ActionGeometry:{width:600,height:600},label:'本地时间',seconds:true,digital:true,sweep:false},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'monoflip',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'黑白翻页时钟':'Monochrome Flip Clock',Tooltip:'Six-digit flip clock'}])),Settings:{ActionGeometry:{width:1200,height:448},animate:true,duration:400,transport:'compatible',showDate:true,demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'pixelclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'像素点阵时钟':'Pixel Matrix Clock',Tooltip:'Square LED pixel clock'}])),Settings:{ActionGeometry:{width:1100,height:320},showSeconds:true,blink:true,color:'#f4f8ff',brightness:90,glow:true},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'dualflip',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'双联翻页时钟':'Wide Flip Clock',Tooltip:lang.startsWith('zh')?'宽牌小时与分钟翻页时钟':'Wide hour and minute flip clock'}])),Settings:{ActionGeometry:{width:1420,height:420},animate:true,duration:450,transport:'compatible',showDate:true,hour12:false,demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'origamiclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'折纸带时钟':'Origami Ribbon Clock',Tooltip:lang.startsWith('zh')?'尖角纸带与逐段折叠动画':'Folded ribbon digits with segment animation'}])),Settings:{ActionGeometry:{width:1200,height:420},theme:'red',animate:true,duration:650,blink:true,showDate:true,transport:'compatible',demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'continuousribbon',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'连续纸带时钟':'Continuous Ribbon Clock',Tooltip:lang.startsWith('zh')?'纸带路径连续变形成数字':'A continuous ribbon morphs between digits'}])),Settings:{ActionGeometry:{width:1200,height:420},theme:'red',animate:true,duration:1100,blink:true,showDate:true,breath:true,transport:'compatible',demoToken:0},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'techdial',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'科技仪表时钟':'Tech Dial Clock',Tooltip:lang.startsWith('zh')?'秒钟驱动的环形光条仪表':'Instrument dial with a second-driven light ring'}])),Settings:{ActionGeometry:{width:1200,height:400},theme:'orange',smooth:true,showSeconds:true,showDate:true,railFlow:true,glow:85,transport:'compatible'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'triplering',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'三层时间圆环':'Triple Time Rings',Tooltip:lang.startsWith('zh')?'小时、分钟与秒钟三层进度环':'Concentric hour, minute and second progress rings'}])),Settings:{ActionGeometry:{width:1200,height:400},theme:'orange',smooth:true,showSeconds:true,showDate:true,labels:true,glow:85,transport:'compatible'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'calendarflip',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'木纹日历翻页时钟':'Wood Calendar Flip Clock',Tooltip:lang.startsWith('zh')?'月日时分秒五联翻牌':'Five-panel month, day, hour, minute and second clock'}])),Settings:{ActionGeometry:{width:1920,height:462},animate:true,duration:440,hour12:false,labels:'en',wood:'maple',transport:'compatible'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
Actions.push({UUID:'woodphotoclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'高清木纹翻页时钟':'HD Wood Flip Clock',Tooltip:lang.startsWith('zh')?'使用高清材质底图的五联翻页时钟':'Five-panel flip clock with an HD material background'}])),Settings:{ActionGeometry:{width:1920,height:462},animate:true,duration:440,hour12:false,transport:'compatible'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
// !! Do not modify below — autofile.cjs depends on this shape !!
module.exports = { PUUID: Plugin.UUID, Version: Plugin.version, APIVersion: Plugin.APIVersion, CategoryIcon: Plugin.Icon, i18n: Plugin.i18n, Actions };
