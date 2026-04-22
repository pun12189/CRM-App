;Example .NET Setup Script
;written in Inno Setup 5.1.9 (ISPP 5.1.8.0)
;2008 Matthew Kleinwaks
;www.ZerosAndTheOne.com
;change these comments as needed for your own app
;#ifndef IncludeFramework  
;#define public IncludeFramework "False"
;#endif
;#ifndef Version
;#define public Version "1.0.0.25"
;#endif
;#ifndef SourceFileDir
;#define public SourceFileDir "C:\Users\Kishant\Source\Repos\KlikCRM\bin\Release\net6.0-windows\"
;#endif

#include "CodeDependencies.iss"

[setup]
;name of your application
ChangesAssociations=yes 
ChangesEnvironment=yes
PrivilegesRequired=admin
DisableReadyPage=yes
AppName=KlikCRM      
;DefaultDialogFontName=Segoe UI     
;repeat name of application. (otherwise you get
;multiple entries in add/remove programs)
AppVerName=KlikCRM
AppId=KlikCRM
AppCopyright=Copyright © 2024
;app publisher name
AppPublisher=sofric.com
;app publisher website URL
AppPublisherURL=https://sofric.com
;app publisher support URL
AppSupportURL=https://sofric.com/support
;app publisher updates URL
AppUpdatesURL=https://sofric.com/download
;default directory {pf} is a constant for
;program files. See INNO help for all constants
DefaultDirName={autopf}\KlikCRM
;default group name in the programs
; section of the start menu
DefaultGroupName=KlikCRM
;Boolean to disable allowing user to customize
;start menu entry during installation
DisableProgramGroupPage=yes
;Boolean to warn if directory user picks
;aaalready exists
UsePreviousAppDir=no
DirExistsWarning=no
UsePreviousGroup=no
;directory where uninstaller exe will be
;this will be where our app is
;the constant we use is {app}
UninstallFilesDir={app}
;Location of the license file
LicenseFile={#SourceFileDir}SetupFiles\eula.rtf
;file to show before install (I show sys requirements)
;InfoBeforeFile={#SourceFileDir}sysreq.rtf
;file to show after install (I show readme)
;InfoAfterFile={#SourceFileDir}readme.txt
;Custom image to show on left side of installer
WizardImageFile={#SourceFileDir}SetupFiles\BahiKitab-Icon.bmp
WizardSmallImageFile={#SourceFileDir}SetupFiles\BahiKitab-Icon.bmp
;I use whatever my apps icon is                               
;UninstallDisplayIcon={app}\BahiKitab.exe
;Version number of your installer (not your app)
VersionInfoVersion={#Version}
AppVersion={#Version}
;If IncludeFramework, append _FW to end of compiled setup;
ArchitecturesAllowed = x86compatible and x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
;without the framework included
OutputBaseFilename=KlikCRM
;Directory where setup.exe will be compiled to
;OutputDir="C:\Users\Kishant\OneDrive\Desktop\setup"
WizardStyle=modern
WindowShowCaption=no 
WindowResizable=yes
SetupIconFile={#SourceFileDir}SetupFiles\BahiKitab-Icon.ico
SetupLogging=yes
;#include <idp.iss>
 ;SignTool=sn /d $qTemplateToaster Installer$q $f
 UninstallIconFile={#SourceFileDir}SetupFiles\BahiKitab-Icon.ico
 [Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
;Name: "da"; MessagesFile: "compiler:Languages\Danish.isl"
;Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
;Name: "fr"; MessagesFile: "compiler:Languages\French.isl" 
;Name: "de"; MessagesFile: "compiler:Languages\German.isl"
;Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
;Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
;Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
;Name: "fi"; MessagesFile: "compiler:Languages\Finnish.isl"
;Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
;Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
;Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"

;Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

; Name: "he"; MessagesFile: "compiler:Languages\Hebrew.isl"

; Name: "no"; MessagesFile: "compiler:Languages\Norwegian.isl"



;Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
; Name: "sv"; MessagesFile: "compiler:Languages\Swedish.isl"
;Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl" 
;Name: "zh"; MessagesFile: "compiler:Languages\Chinese.isl"

  ;Name: "el"; MessagesFile: "compiler:Languages\Greek.isl"
 ; Name: "fi"; MessagesFile: "compiler:Languages\Finnish.isl"
;Name: "hu"; MessagesFile: "compiler:Languages\Hungarian.isl"

 ; Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
 ; Name: "ro"; MessagesFile: "compiler:Languages\Romanian.isl"
 ; Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"


   [Dirs]
 Name: "{app}\de"
 Name: "{commonappdata}\KlikCRM"; Permissions: everyone-modify
 Name: "{app}\TxtFile"
 Name: "{app}\runtimes"
[Files]
   Source: {#SourceFileDir}\de\*;  DestDir: "{app}\de"; Flags:ignoreversion recursesubdirs
   Source: {#SourceFileDir}\runtimes\*;  DestDir: "{app}\runtimes"; Flags:ignoreversion recursesubdirs
   Source: {#SourceFileDir}\TxtFile\*;  DestDir: "{app}\TxtFile"; Flags:ignoreversion recursesubdirs
   Source: {#SourceFileDir}\BahiKitab.exe;  DestDir: "{app}"; Flags:ignoreversion recursesubdirs
   Source: {#SourceFileDir}\*.dll;  DestDir: "{app}"; Flags:ignoreversion
   Source: {#SourceFileDir}\*.pdb;  DestDir: "{app}"; Flags:ignoreversion
   Source: {#SourceFileDir}\*.json;  DestDir: "{app}"; Flags:ignoreversion
   ;Source: {userappdata}\TemplateToaster\Fonts\*; DestDir: "{userappdata}\TemplateToaster\Fonts 6.0"; Flags:external recursesubdirs uninsneveruninstall skipifsourcedoesntexist; Check : CreateDestinationDir(ExpandConstant('{userappdata}\TemplateToaster\Fonts 6.0'));

   ;Source: {#SourceFileDir}Fonts\*;   DestDir: "{app}\Fonts"; Flags:ignoreversion recursesubdirs

  ;Source: {#SourceFileDir}Fonts\DidactGothic.TTF; DestDir: "{fonts}"; FontInstall: "Didact Gothic"; Flags: onlyifdoesntexist uninsneveruninstall
  ; Source: {#SourceFileDir}Fonts\Dosis-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Dosis"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Fondamento-Italic.TTF; DestDir: "{fonts}"; FontInstall: "Fondamento"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Fondamento-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Fondamento"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Forum-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Forum"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Marmelad-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Marmelad"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Nunito-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Nunito"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Playball-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Playball"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\PoiretOne-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Poiret One"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Righteous-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Righteous"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\UbuntuCondensed-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Ubuntu Condensed"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\Ubuntu-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Ubuntu"; Flags: onlyifdoesntexist uninsneveruninstall
   ;Source: {#SourceFileDir}Fonts\YanoneKaffeesatz-Regular.OTF; DestDir: "{fonts}"; FontInstall: "Yanone Kaffeesatz Regular"; Flags: onlyifdoesntexist uninsneveruninstall
    ;Source: {#SourceFileDir}Fonts\Aclonica.TTF; DestDir: "{fonts}"; FontInstall: "Aclonica"; Flags: onlyifdoesntexist uninsneveruninstall
     ;Source: {#SourceFileDir}Fonts\Amarante-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Amarante"; Flags: onlyifdoesntexist uninsneveruninstall
      ;Source: {#SourceFileDir}Fonts\CantoraOne-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Cantora One"; Flags: onlyifdoesntexist uninsneveruninstall
      ;Source: {#SourceFileDir}Fonts\Capriola-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Capriola"; Flags: onlyifdoesntexist uninsneveruninstall
      ;Source: {#SourceFileDir}Fonts\Courgette-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Courgette"; Flags: onlyifdoesntexist uninsneveruninstall
      ;Source: {#SourceFileDir}Fonts\Handlee-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Handlee"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Homenaje-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Homenaje"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Junge-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Junge"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Lemon-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Lemon"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\MedulaOne-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Medula One"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\NovaOval.TTF; DestDir: "{fonts}"; FontInstall: "Nova Oval"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Oranienbaum-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Oranienbaum"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Oregano-Italic.TTF; DestDir: "{fonts}"; FontInstall: "Oregano"; Flags: onlyifdoesntexist uninsneveruninstall
       ;Source: {#SourceFileDir}Fonts\Oregano-Regular.TTF; DestDir: "{fonts}"; FontInstall: "Oregano"; Flags: onlyifdoesntexist uninsneveruninstall
       ;;Source: {#SourceFileDir}Fonts\StintUltraCondensed-Regular.TTF; DestDir: "{fonts}}"; FontInstall: "Stint Ultra Condensed"; Flags: onlyifdoesntexist uninsneveruninstall
     
       ;Source: {#SourceFileDir}xulrunner\*; DestDir: "{app}\xulrunner";   Flags:ignoreversion recursesubdirs
    
     
  
  
       
       ;Source: {#SourceFileDir}SA\TemplateToaster.exe;  DestDir: "{app}";   Flags:ignoreversion
       ;Source: {#SourceFileDir}Sa\Deactivator.exe;  DestDir: "{app}";     Flags:ignoreversion

;Source: {#SourceFileDir}SA\da\* ;DestDir:"{app}\da" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\nl\* ;DestDir:"{app}\nl" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\fr\* ;DestDir:"{app}\fr" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\de\* ;DestDir:"{app}\de" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\it\* ;DestDir:"{app}\it" ;   Flags:ignoreversion
;Source: {#SourceFileDir}SA\ja\* ;DestDir:"{app}\ja" ;   Flags:ignoreversion
;Source: {#SourceFileDir}SA\pt\* ;DestDir:"{app}\pt" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\es\* ;DestDir:"{app}\es" ; Flags:ignoreversion
;;Source: {#SourceFileDir}SA\fi\* ;DestDir:"{app}\fi" ; Flags:ignoreversion
;Source: {#SourceFileDir}SA\ar\* ;DestDir:"{app}\ar" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\cs\* ;DestDir:"{app}\cs" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\pl\* ;DestDir:"{app}\pl" ;  Flags:ignoreversion
;Source: {#SourceFileDir}SA\tr\* ;DestDir:"{app}\tr" ;  Flags:ignoreversion

 ;Source: {#SourceFileDir}SA\de\* ;DestDir:"{app}\de" ;  Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\es\* ;DestDir:"{app}\es" ;  Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\fr\* ;DestDir:"{app}\fr" ;  Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\en\* ;DestDir:"{app}\en" ;  Flags:ignoreversion


 ;Source: {#SourceFileDir}SA\he\* ;DestDir:"{app}\he" ;  Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\it\* ;DestDir:"{app}\it" ;   Flags:ignoreversion


 ;Source: {#SourceFileDir}SA\ko\* ;DestDir:"{app}\ko" ;   Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\no\* ;DestDir:"{app}\no" ;   Flags:ignoreversion
 
 
 
 ;Source: {#SourceFileDir}SA\ru\* ;DestDir:"{app}\ru" ;  Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\sv\* ;DestDir:"{app}\sv" ;  Flags:ignoreversion
 
 ;Source: {#SourceFileDir}SA\zh-CHS\* ;DestDir:"{app}\zh-CHS" ;   Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\zh-CHT\* ;DestDir:"{app}\zh-CHT" ;   Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\zh-Hans\* ;DestDir:"{app}\zh-Hans" ; Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\zh-Hant\* ;DestDir:"{app}\zh-Hant" ; Flags:ignoreversion

 ;Source: {#SourceFileDir}SA\el\* ;DestDir:"{app}\el" ;    Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\fi\* ;DestDir:"{app}\fi" ;    Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\hu\* ;DestDir:"{app}\hu" ;     Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\ja\* ;DestDir:"{app}\ja" ;     Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\ko\* ;DestDir:"{app}\ko" ;    Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\ro\* ;DestDir:"{app}\ro" ;   Flags:ignoreversion
 ;Source: {#SourceFileDir}SA\uk\* ;DestDir:"{app}\uk" ;   Flags:ignoreversion

;Source: {#SourceFileDir}Files\WpfApplication97.pdb; DestDir: {app}; Flags: ignoreversion {#IsExternal}
;Source: {#SourceFileDir}Files\WpfApplication97.vshost.exe; DestDir: {app} ;Flags: ignoreversion {#IsExternal}
;Source: {#SourceFileDir}Files\WpfApplication97.vshost.exe.manifest; DestDir: {app}; Flags: ignoreversion {#IsExternal}
;source: {#SourceFileDir}readme.txt;    DestDir: {app}; Flags: ignoreversion {#IsExternal}

;Source: {#SourceFileDir}SetupFiles\windowsdesktop-runtime-6.0.36-win-x64.exe; DestDir: {tmp}; Flags: deleteafterinstall;
;Source: {#SourceFileDir}SetupFiles\windowsdesktop-runtime-6.0.36-win-x86.exe; DestDir: {tmp}; Flags: deleteafterinstall;

;Source: {#SourceFileDir}SetupFiles\net8runtime.exe; DestDir: {tmp}; Flags: deleteafterinstall
;Source: {#SourceFileDir}SetupFiles\NDP40-KB2468871-v2-x86.exe; DestDir: {tmp}; Check: Not IsWin64
;[Types]
;Name: "full"; Description: "Full installation"; Flags: iscustom
;Name: "compact"; Description: "Compact installation" 




;[Components]
;Name: "main"; Description: "Main Files"; Types: full compact custom; Flags: fixed
;Name: "compact"; Description: "Compact installation"; Types: full 
;[Tasks]
;Name: starterversion; Description: "Starter Version"; Flags:exclusive 
;Name: standardversion; Description: "{cm:StandardVersion}";   Flags:exclusive unchecked;  
;Name: professionalversion; Description: "{cm:ProfessionalVersion}";  Flags:exclusive;
;Name: Basic; Description: {#Strd} ; GroupDescription: {#sver}; Flags:exclusive unchecked;  Components: main
;Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
;Name: Professional; Description: {#Pro}; GroupDescription: {#sver}; Flags: exclusive;
;Name: desktopicon\user; Description: "For the current user only"; GroupDescription: "Additional icons:"; Components: main; Flags: exclusive unchecked
;Name: quicklaunchicon; Description: "Create a &Quick Launch icon"; GroupDescription: "Additional icons:"; Components: main; Flags: unchecked
;Name: associate; Description: "&Associate files"; GroupDescription: "Other tasks:"; Flags: unchecked
[CustomMessages]
;StandardVersion=Standard Edition
;StarterVersion=Starter Edition
;ProfessionalVersion=Professional Edition
InstallingDependencies=Installing Dependencies
viewhelpfile = View Help File
optimizingperformance = Optimizing performance
launchtemplatetoaster = Launch KlikCRM
settingpermission = Setting Program Access Permissions

;da.StandardVersion=Standard Edition
;da.StarterVersion=Starter Edition
;da.ProfessionalVersion=professionel Edition
;da.InstallingDependencies=Installation AfhÃ¦ngigheder
;da.optimizingperformance=optimering af ydeevne
;da.launchtemplatetoaster=Launch TemplateToaster
;da.settingpermission= Indstilling Program Adgangstilladelser

;nl.StandardVersion=Standaard editie
;nl.StarterVersion=Starter editie
;nl.ProfessionalVersion=Professionele editie
;nl.InstallingDependencies=Afhankelijkheden installeren
;nl.optimizingperformance= Optimaliseren van prestaties
;nl.launchtemplatetoaster=TemplateToaster starten
;nl.settingpermission= Toegangsrechten voor programma's instellen
;nl.viewhelpfile = Helpbestand bekijken

;fr.StandardVersion=Standard Édition
;fr.StarterVersion=Entrée Édition
;fr.ProfessionalVersion=Professionnel Édition
;fr.InstallingDependencies= Installation des dépendances
;fr.optimizingperformance=optimisation des performances
;fr.launchtemplatetoaster=Lancement TemplateToaster
;fr.settingpermission=Réglage Programme des autorisations d'accès

;de.StandardVersion=Standard Edition
;de.StarterVersion=Starter Ausgabe
;de.ProfessionalVersion=Professionelle Version
;de.InstallingDependencies=Dependencies installieren
;de.optimizingperformance=Optimalisierung der Leistung
;de.launchtemplatetoaster=TemplateToaster starten
;de.settingpermission= Festlegen von Programmzugriffsberechtigungen
;de.viewhelpfile = Hilfedatei ansehen
 
;it.StandardVersion=edizione Standard
;it.StarterVersion=edizione Starter
;it.ProfessionalVersion= edizione Professional
;it.InstallingDependencies=Installazione Dipendenze
;it.optimizingperformance=Ottimizzazione delle prestazioni
;it.launchtemplatetoaster=Lancio TemplateToaster
;it.settingpermission= Impostazione delle autorizzazioni di accesso del programma
 
;ja.StandardVersion=スタンダード版
;ja.StarterVersion=初心者モード
;ja.ProfessionalVersion= プロフェッショナル・エディション
;ja.InstallingDependencies=依存関係をインストールします
;ja.optimizingperformance=パフォーマンスの最適化 
;ja.launchtemplatetoaster=起動TemplateToaster
;ja.settingpermission= プログラムのアクセス許可の設定
 
;pt.StandardVersion=Edição Padrão
;pt.StarterVersion=starter Edition
;pt.ProfessionalVersion=Professional Edition
;pt.InstallingDependencies=Instalação Dependências
;pt.optimizingperformance=otimizar o desempenho
;pt.launchtemplatetoaster=Iniciar o TemplateToaster
;pt.settingpermission= Definir Programa de permissões de acesso

;es.optimizingperformance=Optimización del rendimiento
;es.launchtemplatetoaster=TemplateToaster Lanzamiento
;es.settingpermission= Configuración del programa de permisos de acceso
;es.InstallingDependencies=Instalación de dependencias
;es.StandardVersion=edición estándar
;es.StarterVersion=Motor de arranque Edición;

;fi.StandardVersion=standardi Painos
;fi.StarterVersion=Käynnistin Painos
;fi.ProfessionalVersion = ammatillinen Painos
;fi.InstallingDependencies= asentaminen riippuvuudet
;fi.optimizingperformance= optimointiin
;fi.launchtemplatetoaster= Käynnistää TemplateToaster
;fi.settingpermission = Asetusohjelma Käyttöoikeudet

;ar.StandardVersion=الإصدار الأساسى
;ar.StarterVersion=كاتب الطبعة 
;ar.ProfessionalVersion=المحترف
;ar.InstallingDependencies=تثبيت التبعيات
;ar.optimizingperformance =الأداء الأمثل
;ar.launchtemplatetoaster =إطلاق TemplateToaster
;;r.settingpermission =وضع ضوابط الوصول إلى البرامج

;pl.StandardVersion=Wydanie standardowe
;pl.StarterVersion= Rozrusznik Wydanie 
;pl.ProfessionalVersion= Wydanie profesjonalne
;pl.InstallingDependencies= Instalacja Zależności 
;pl.optimizingperformance = wydajność Optymalizacja
;pl.launchtemplatetoaster = Uruchom TemplateToaster
;pl.viewhelpfile = Pokaż plik pomocy
;pl.settingpermission = Ustawianie program Access Uprawnienia

;hu.StandardVersion = Ingyenes verzió
;hu.ProfessionalVersion = Professzionális verzió
;hu.InstallingDependencies = Mellékletek telepítése
;hu.viewhelpfile = Nézze meg a segítség fájlt
;hu.optimizingperformance = Teljesítmény optimalizálása
;hu.launchtemplatetoaster = ITemplateToaster elindítása
;hu.settingpermission = Program hozzáférési engedélyek beállítása

;ru.StandardVersion=Стандартная версия
;ru.StarterVersion=Стартер Издание
;ru.ProfessionalVersion=профессиональный Издание
;ru.InstallingDependencies=Установка зависимостей
;ru.viewhelpfile = Просмотр файла справки
;ru.optimizingperformance = Оптимизация производительности
;ru.launchtemplatetoaster = Запустить TemplateToaster
;ru.settingpermission = Настройка разрешений на доступ к программе;

;zh.StandardVersion=æ ‡å‡† ç‰ˆæœ¬
;zh.StarterVersion=èµ·åŠ¨æœº ç‰ˆæœ¬;

;cs.StandardVersion=Norma Vydání
;cs.StarterVersion=Starter Vydání
;cs.ProfessionalVersion=Profesionální Vydání
;cs.InstallingDependencies=Instalace závislostí;

;tr.StandardVersion=standart Sürüm
;tr.StarterVersion=Starter Sürüm
;tr.InstallingDependencies= bağımlılıklar Takma
;tr.optimizingperformance = Optimize performansı
;tr.launchtemplatetoaster = Başlat TemplateToaster
;tr.settingpermission = Program Erişim İzinlerini Ayarlama

;he.StandardVersion=×’×¨×¡×” ×¡×˜× ×“×¨×˜×™×ª
;he.StarterVersion=Starter ×¨×˜×™×ª

;no.StandardVersion=standard versjon
;no.StarterVersion=Starter versjon

;sv.StandardVersion=Standard Version
;sv.StarterVersion=Starter Version

;el.StandardVersion=πρότυπο Έκδοση·
;el.StarterVersion=Δημιουργός Έκδοση·

;fi.StandardVersion=standardi versio
;fi.StarterVersion=käynnistin versio

;ko.StandardVersion=표준 버전
;ko.StarterVersion=경주에 나가는 말 버전

;ro.StandardVersion=Standard versiune
;ro.StarterVersion=Starter versiune

;uk.StandardVersion=стандарт версія
;uk.StarterVersion=стартер версія
[icons]
Name: {group}\KlikCRM; Filename: {app}\BahiKitab.exe; WorkingDir: {app}; IconFilename: "{#SourceFileDir}SetupFiles\BahiKitab-Icon.ico";
Name: "{group}\Help"; Filename: "http://sofric.com/"; 
Name: {group}\Uninstall; Filename: {uninstallexe};  WorkingDir: {app}; IconFilename: "{#SourceFileDir}SetupFiles\BahiKitab-Icon.ico";
Name: {commondesktop}\KlikCRM;  Filename: {app}\BahiKitab.exe; IconFilename: "{#SourceFileDir}SetupFiles\BahiKitab-Icon.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"

[Run]
;Filename: "{tmp}\net8runtime.exe"; Parameters: "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"; Flags: runhidden waituntilterminated
Filename: "{app}\BahiKitab.exe"; Description: "{cm:launchtemplatetoaster}"; Flags: nowait skipifsilent postinstall ; 
Filename: explorer.exe; Parameters: "http://sofric.com/"; Description: "{cm:viewhelpfile}"; Flags: Shellexec skipifsilent  postinstall unchecked; 

;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Edition /t REG_SZ /d ""Starter"" /f"; Flags: runasoriginaluser; Tasks: starterversion
;Filename: Reg.exe; Parameters: "add ""HKLM\Software\TemplateToaster"" /v Edition /t REG_SZ /d ""Standard"" /f"; Flags: runasoriginaluser runhidden;  Tasks: standardversion
;Filename: Reg.exe; Parameters: "add ""HKLM\Software\TemplateToaster"" /v Edition /t REG_SZ /d ""Professional"" /f"; Flags: runasoriginaluser runhidden;  Tasks: professionalversion
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Edition /t REG_SZ /d ""Standard"" /f"; Flags: runasoriginaluser runhidden;  Tasks: standardversion
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Edition /t REG_SZ /d ""Professional"" /f"; Flags: runasoriginaluser runhidden;  Tasks: professionalversion
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v installpath /t REG_SZ /d ""{app}"" /f"; Flags: runasoriginaluser;

Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""8000"" /f"; Check : IsCompatibleIExplorerVersion('7');
Filename: Reg.exe; Parameters: "add ""HKLM\SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""8000"" /f";Flags:64bit; Check : IsCompatibleIExplorerVersionWin64('7');

Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""8000"" /f"; Check : IsCompatibleIExplorerVersion('8');
Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""8000"" /f";Flags:64bit; Check : IsCompatibleIExplorerVersionWin64('8');

Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""9000"" /f"; Check : IsCompatibleIExplorerVersion('9');
Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""9000"" /f";Flags:64bit; Check : IsCompatibleIExplorerVersionWin64('9');

Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""10000"" /f"; Check : IsCompatibleIExplorerVersion('10');
Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""10000"" /f";Flags:64bit; Check : IsCompatibleIExplorerVersionWin64('10');

Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""11000"" /f"; Check : IsCompatibleIExplorerVersion('11');
Filename: Reg.exe; Parameters: "add ""HKLM\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"" /v BahiKitab.exe /t REG_DWORD /d ""11000"" /f";Flags:64bit; Check : IsCompatibleIExplorerVersionWin64('11');

;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""en"" /f"; Flags: runasoriginaluser runhidden;  Languages: en
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""da"" /f"; Flags: runasoriginaluser runhidden;  Languages: da
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""nl"" /f"; Flags: runasoriginaluser runhidden;  Languages: nl
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""fr"" /f"; Flags: runasoriginaluser runhidden;  Languages: fr
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""de"" /f"; Flags: runasoriginaluser runhidden;  Languages: de
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""it"" /f"; Flags: runasoriginaluser runhidden;  Languages: it
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""ja"" /f"; Flags: runasoriginaluser runhidden;  Languages: ja
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""pt"" /f"; Flags: runasoriginaluser runhidden;  Languages: pt
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""fi"" /f"; Flags: runasoriginaluser runhidden;  Languages: fi
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""pl"" /f"; Flags: runasoriginaluser runhidden;  Languages: pl

 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""ar"" /f"; Flags: runasoriginaluser runhidden;  Languages: ar
 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""cs"" /f"; Flags: runasoriginaluser runhidden;  Languages: cs 

 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""es"" /f"; Flags: runasoriginaluser runhidden;  Languages: es

 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""he"" /f"; Flags: runasoriginaluser runhidden;  Languages: he

 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""no"" /f"; Flags: runasoriginaluser runhidden;  Languages: no



 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""ru"" /f"; Flags: runasoriginaluser runhidden;  Languages: ru
 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""sv"" /f"; Flags: runasoriginaluser runhidden;  Languages: sv
  ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""tr"" /f"; Flags: runasoriginaluser runhidden;  Languages: tr
 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""zh-Hans"" /f"; Flags: runasoriginaluser runhidden;  Languages: zh



   ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""el"" /f"; Flags: runasoriginaluser runhidden;  Languages: el
 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""fi"" /f"; Flags: runasoriginaluser runhidden;  Languages: fi
;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""hu"" /f"; Flags: runasoriginaluser runhidden;  Languages: hu

 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""ko"" /f"; Flags: runasoriginaluser runhidden;  Languages: ko
 ; Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""ro"" /f"; Flags: runasoriginaluser runhidden;  Languages: ro
 ;Filename: Reg.exe; Parameters: "add ""HKCU\Software\TemplateToaster"" /v Language /t REG_SZ /d ""uk"" /f"; Flags: runasoriginaluser runhidden;  Languages: uk
 
 
 [InstallDelete]
    
;Type: filesandordirs; Name: "{app}"       
Type: files; Name: "{autoappdata}\IconCache.db";  
Type: filesandordirs; Name: "{app}\Resources";
 [UninstallDelete]
    
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{app}\Resources"
Type:files;  Name:{commondesktop}\KlikCRM
[UninstallRun]
;Filename: {win}\Microsoft.NET\Framework\v4.0.30319\CasPol.exe; Parameters: "-q -machine -remgroup ""KlikCRM"""; Flags: skipifdoesntexist runhidden;
;Filename: {win}\Microsoft.NET\Framework\v4.0.30319\CasPol.exe; Parameters: "-q -machine -remgroup ""KlikCRM"""; Flags: skipifdoesntexist runhidden;
;Filename: {app}\Deactivator.exe; 
;Filename: "https://templatetoaster.com/survey5/survey.php/?v={# Version}"; Flags: shellexec  waituntilterminated

[Registry]
Root: HKCR; Subkey: ".ttr"; ValueType: string; ValueName: ""; ValueData: "KlikCRM"; Flags: uninsdeletevalue 
Root: HKCR; Subkey: "KlikCRM"; ValueType: string; ValueName: ""; ValueData: "KlikCRM"; Flags: uninsdeletekey 
Root: HKCR; Subkey: "KlikCRM\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\BahiKitab.exe,1" 
Root: HKCR; Subkey: "KlikCRM\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\BahiKitab.exe"" ""%1""" 
;Root: HKLM; Subkey: "SOFTWARE\Platform\Brand"; ValueType: string; ValueName: "Colours7"; ValueData:  "{code:RegisteredUser}"; 
 [Code]
   
    var
 flag : Boolean;
//function AppDataFolder(Param: String): String;
//begin
  //if IsTaskSelected('standard') then
    //Result := 'Standard'
  //else
    //Result := 'Professional'
//end; 

//procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
 //var
 //rcode: Integer;
//begin

    // if((CurUninstallStep = usAppMutexCheck) and not UninstallSilent) then
    // ShellExec('', ExpandConstant('{app}\Deactivator.exe'), '','', SW_SHOW, ewNoWait, rcode);
    
 //   if((CurUninstallStep = usAppMutexCheck) and not UninstallSilent) then
   // begin
     //   if ShellExec('', ExpandConstant('{app}\TemplateToaster.exe'), 'Deactivate','', SW_SHOW, ewWaitUntilTerminated, rcode) then
        //begin
       //     if rcode = 0 then
          //    begin
            //    MsgBox('TemplateToaster is successfully deactivated.', mbInformation, MB_OK);
              //end
            //else begin
              //  MsgBox('TemplateToaster is not deactivated properly. Please contact the support at https://templatetoaster.com/support/', mbError, MB_OK);
             //end;
        //end;
    //end;
    
  // if((CurUninstallStep = usDone) and not UninstallSilent) then
    //    ShellExec('open', 'https://templatetoaster.com/survey5/survey.php/?v={# Version}', '', '', SW_SHOW, ewWaitUntilTerminated, rcode);

//end;

//-----------------------------------------------------------------------------------
function IsDotNet461DetectInstall(): Boolean;
var
  Version: TWindowsVersion;
  success: boolean;
  Release: cardinal;
Begin
  GetWindowsVersionEx(Version);
  if (((Version.Build >= 10240) and (Version.Build < 14393)) or ((Version.Major = 6) and (Version.Minor = 2) and (Version.Build = 9200))) then
  Begin
       success := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
       if(success and (Release >= 394254)) then
       Begin          
         Result:=False;          
       End
       else
         Result:= True
  End
  else
  	Result := False;
End;

//-----------------------------------------------------------------------------------
function IsDotNet48DetectInstall(): Boolean;
var
  Version: TWindowsVersion;
  success: boolean;
  Release: cardinal;
Begin
  GetWindowsVersionEx(Version);
  if ((Version.Build >= 14393) or ((Version.Major = 6) and (Version.Minor = 3) and (Version.Build = 9200)) or (Version.Build = 7601)) then
  Begin
       success := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
       if(success and (Release >= 528040))then
       Begin          
         Result:=False;          
       End
       else
         Result:= True
  End
  else
  	Result := False;
End;

//-----------------------------------------------------------------------------------

function RegisteredUser(Param: String): string;
 var 
  FilePath: string;
  begin
   
    FilePath := ExpandConstant('{app}')+'\BahiKitab.exe'  ;
    Result := GetSHA1OfFile(FilePath);
end;

function IsCompatibleIExplorerVersion(IEver : string): Boolean;
var
  sVersion:  String;
  tempmajorVersion : String;
  majorVersion : longint;
  index : integer;
begin
  
RegQueryStringValue( HKLM, 'SOFTWARE\Microsoft\Internet Explorer', 'svcVersion', sVersion );
if(Length(sVersion) = 0) then
  RegQueryStringValue( HKLM, 'SOFTWARE\Microsoft\Internet Explorer', 'Version', sVersion );
    
  Begin
     index := Pos('.', sVersion);
    tempmajorVersion := Copy(sVersion, 1, index - 1);
     majorVersion := StrToInt(tempmajorVersion);
    if(majorVersion = StrToInt(IEver)) then
    Begin
        Result:=True;
    End
    else
        Result:=False;
  End
  
          // special check for less than 7 ie ver
 if(majorVersion < 7) then
Begin

    if(StrToInt(IEver)=7) then
    Result := True;

End
  
end;


function IsCompatibleIExplorerVersionWin64(IEver : string): Boolean;
var
  sVersion:  String;
  tempmajorVersion : String;
  majorVersion : longint;
  index : integer;
begin
  
RegQueryStringValue( HKLM, 'SOFTWARE\Microsoft\Internet Explorer', 'svcVersion', sVersion );
if(Length(sVersion) = 0) then
  RegQueryStringValue( HKLM, 'SOFTWARE\Microsoft\Internet Explorer', 'Version', sVersion );
     
  
     index := Pos('.', sVersion);
      tempmajorVersion := Copy(sVersion, 1, index - 1);
     majorVersion := StrToInt(tempmajorVersion);
    if(majorVersion = StrToInt(IEver)) then
    Begin
        Result:=True;
    End
    else
        Result:=False;
 
    
          // special check for less than 7 ie ver
 if(majorVersion < 7) then
Begin

    if(StrToInt(IEver)=7) then
    Result := True;
      end;


if Not IsWin64 then
Result:=False;
end;


function CreateDestinationDir(DirName: String): Boolean;
Begin
if(flag = true) then
	begin
		Result:=True;
	end
else
    begin 
		if(not DirExists(DirName)) then
			Begin
				CreateDir(DirName);
				flag := true;
				Result := True;
			End
		else
			Begin
				Result := False;
			End 
	end
End;

//CopyUiautomationdll If windows 7 64 bit Version...

     function CopyDll(): Boolean;
 var
  Version: TWindowsVersion;
begin 
GetWindowsVersionEx(Version);  
if  IsWin64   
   then
  begin
     if FileExists(ExpandConstant('{syswow64}\UIAutomationCore.dll')) then      
       begin
        Result:= False;
       end
         else
            begin
            if (Version.Major = 6) and
     (Version.Minor = 1)  then          
           begin
            Result:= True;
           end
           else
           begin
           Result := False;
            end;
            end;
     end
  else
     begin
       Result := False;
     end;
end;

function IsDotNetVersion4(): boolean;
var
netVersion: String; 
version: Integer;
temp: String;
isVersion4:   boolean;
 begin
         isVersion4:=False;
     RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client', 'Version', netVersion);
     
     temp := Copy(netVersion, 3, 1);
     version := StrToInt(temp);
     if(version = 0) then
     begin
isVersion4:=True;
end;
Result := isVersion4;
end; 

function IsInstallOnWin7Win8(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result :=
    (Version.Major = 6);
end;

// Indicates whether KB2468871 64 is installed.
// function IsKB246887164Detected(): boolean;
// begin
// if IsWin64 then
// begin
// Result := ((Not RegKeyExists(HKLM, 'SOFTWARE\Wow6432Node\Microsoft\Updates\Microsoft .NET Framework 4 Client Profile\KB2468871')) and (IsDotNetVersion4()));    
// end
// else
// begin
// Result := False;
// end;
// end;

// Indicates whether KB2468871 32 is installed.
// function IsKB246887132Detected(): boolean;
// begin
// if Not IsWin64 then
// begin
// Result := ((Not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Updates\Microsoft .NET Framework 4 Client Profile\KB2468871')) and (IsDotNetVersion4()));    
// end
// else
// begin
// Result := False;
// end;
// end;

// Indicates whether .NET Framework 4.0 is installed.
function IsDotNET40Detected(version: string ): boolean;
var
    success: boolean;
    install: cardinal;
begin
    success := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\'+version, 'Install', install);
    Result := success and (install = 1);
end;

//RETURNS OPPOSITE OF IsDotNet40Detected FUNCTION
//Remember this method from the Files section above
function NeedsFramework(): Boolean;
begin
  Result := ((IsDotNET40Detected('v4\Client') or IsDotNET40Detected('v4\Full') ) = false);
end;

function CheckClient: Boolean;
var
  Version: TWindowsVersion;
begin
GetWindowsVersionEx(Version);
if ((Version.Major = 6) and (Version.Build = 6002)) then
begin
//MsgBox('TemplateToaster requires Microsoft .net framework 4 which is not present on your PC. Installer will now install Microsoft .Net framework 4. It can take several minutes to install', mbInformation, MB_OK);
  Result := True
  end
  else
   Result := False;
  
end;

procedure ExitProcess(exitCode:integer);
  external 'ExitProcess@kernel32.dll stdcall';

//GETS VERSION OF IE INSTALLED ON CLIENT MACHINE
function GetIEVersion : String;
var
  IE_VER: String;
begin
  {First check if Internet Explorer is installed}
  if RegQueryStringValue(HKLM,'SOFTWARE\Microsoft\Internet Explorer','Version',IE_VER) then
      Result := IE_VER
else
    {No Internet Explorer at all}
    result := '';
end;

function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  // get the uninstall string of the old app
  sUnInstallString := RemoveQuotes(GetUninstallString());
  
  Exec(sUnInstallString, ' /VERYSILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode)
    Result := iResultCode;
end;

//IF SETUP FINISHES WITH EXIT CODE OF 0, MEANING ALL WENT WELL
//THEN CHECK FOR THE PRESENCE OF THE REGISTRY FLAG TO INDICATE THE
//.NET FRAMEWORK WAS INSTALLED CORRECTLY
//IT CAN FAIL WHEN CUST DOESN'T HAVE CORRECT WINDOWS INSTALLER VERSION
function GetCustomSetupExitCode(): Integer;
begin
  if NeedsFramework then
    begin
      MsgBox('.NET Framework was NOT installed successfully!',mbError, MB_OK);
      result := -1
    end
end;
//function InitializeSetup(): Boolean;
//var
  //ErrCode: integer;
  //UninsResult : integer;
//begin

  //     UninsResult := UnInstallOldVersion();
       
    //if NeedsFramework() then 
    //begin
      //  if MsgBox('KlikCRM requires Microsoft .NET 6 Desktop Runtime.'#13#13
        //    'Please download this file to run KlikCRM. Click Yes to download it.',  mbConfirmation, MB_YESNO) = IDYES
          //  then 
    //ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.36-windows-x64-installer',
      //'', '', SW_SHOW, ewNoWait, ErrCode);
     
        //result := false;   
        //end 
     //else
       // result := true;
//end;
// THIS FUNCTION IS FOR CHECK THAT VC++ REDIST 2015 OR LATER IS INSTALLED OF NOT
function VCinstalled: Boolean;
 // Function for Inno Setup Compiler
 // Returns True if same or later Microsoft Visual C++ 2015 Redistributable is installed, otherwise False.
 var
 major: Cardinal; 
 key: String;
 begin
 Result := False;
 key := 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
 if RegQueryDWordValue(HKEY_LOCAL_MACHINE, key, 'Major', major) then begin
 
		 Log('VC 2015 Redist Major is: ' + IntToStr(major));
		 // Version info was found. Return true if later or equal to our 14.0.24212.00 redistributable
		 // Note brackets required because of weird operator precendence
		 Result := (major = 14);
 end;
 end;

type
  TIntegerArray = array of String;

procedure ExtractIntegers(Strings: TStrings; out Integers: TIntegerArray);
var
  S: string;
  Ver: string;
  I: Integer;
begin
  for I := 0 to Strings.Count - 1 do
  begin
    // trim the string copied from a substring after the ":" char
    S := Trim(Copy(Strings[I], 1, Pos(':', Strings[I])-1));
    Ver:= '{#SetupSetting("AppVersion")}';

    if not (S=Ver) then
    begin
      SetArrayLength(Integers, GetArrayLength(Integers) + 1);
      Integers[GetArrayLength(Integers) - 1] := Strings[I];
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  I: Integer;
  Strings: TStringList;
  Integers: TIntegerArray;
  FilePath: string;
  Res: String;
begin

if ((CurStep = ssInstall) and IsWin64) then 
   begin
    DelTree(ExpandConstant('{autopf}\KlikCRM'), True, True, True);
end;

if CurStep = ssInstall then 
   begin
    DelTree(ExpandConstant('{app}\KlikCRM\Resources'), True, True, True);
end;

//if CurStep = ssPostInstall then 
  // begin
  //Strings := TStringList.Create;
  //try
  //if FileExists(ExpandConstant('{commonappdata}\TemplateToaster\Colours7.txt')) then
    //begin
    //Strings.LoadFromFile(ExpandConstant('{commonappdata}\TemplateToaster\Colours7.txt'));
    //ExtractIntegers(Strings, Integers);
    //SaveStringsToFile(ExpandConstant('{commonappdata}\TemplateToaster\Colours7.txt'), Integers, False);
    //end;
    // Not necessary, already created in DIRS section with full permissions.
	// if not DirExists(ExpandConstant('{commonappdata}\TemplateToaster')) then begin CreateDir(ExpandConstant('{commonappdata}\TemplateToaster')); end;    
    //FilePath := ExpandConstant('{app}\BahiKitab.exe');
    //Res := GetSHA1OfFile(FilePath);
    //SaveStringToFile(ExpandConstant('{commonappdata}\TemplateToaster\Colours7.txt'), '{#SetupSetting("AppVersion")}' + ':  ' + Res + #13#10, True);
  //finally
    //Strings.Free;
  //end;
  //end;
end;
//-----------------------------------------------------------------------------------
function InitializeSetup: Boolean;
begin
  
  Dependency_AddDotNet80Desktop;   

  Result := True;
end;

procedure InitializeWizard();
var
Version: TWindowsVersion;
ttpath: String;
Begin

// Fresh Installation

  //idpDownloadFile('https://templatetoaster.com', ExpandConstant('{tmp}\abc_check'));
  //if(not FileExists(ExpandConstant('{tmp}\abc_check'))) then
  //Begin
    //MsgBox('Please connect to internet and try again', mbError, MB_OK);
    //ExitProcess(0);
  //End;

  GetWindowsVersionEx(Version);      

  //if IsWin64 and IsKB246167864Detected then
  //Begin    
  	//idpAddFileSize('http://templatetoaster.com/downloads/files/NDP40-KB2461678-v2-x64.exe', ExpandConstant('{tmp}\NDP40-KB2461678-v2-x64.exe'), 5826920);
  //End;

  //if Not IsWin64 and IsKB246167832Detected then
  //Begin	
    //idpAddFileSize('http://templatetoaster.com/downloads/files/NDP40-KB2461678-v2-x86.exe', ExpandConstant('{tmp}\NDP40-KB2461678-v2-x86.exe'), 4753768);
  //End;

  //if IsWin64 and Not VCinstalled then
  //Begin	
    //idpAddFileSize('https://templatetoaster.com/downloads/files/vc_redist.x64.exe', ExpandConstant('{tmp}\vc_redist.x64.exe'), 15302984);
  //End;

  //if Not IsWin64 and Not VCinstalled then
  //Begin	
    //idpAddFileSize('https://templatetoaster.com/downloads/files/vc_redist.x86.exe', ExpandConstant('{tmp}\vc_redist.x86.exe'), 14458272);
  //End;

    //idpDownloadAfter(wpReady);
End;

