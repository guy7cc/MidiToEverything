# Generates installer/Package.wxs — a per-machine MSI with a fully custom,
# multilingual wizard whose FIRST page lets the user pick the UI language.
#
# How localization works in a single MSI: Windows Installer loads UI text once,
# so we cannot swap the built-in WixUI dialog language at runtime. Instead, every
# user-facing string is an MSI property ([S_*]); the first dialog (LangDlg) sets
# all of them via SetProperty control events for the chosen language, and the rest
# of the custom dialogs reference those properties. Secondary/rare dialogs
# (folder browser, progress, errors, maintenance) reuse the built-in WixUI ones.
#
# Run from the repo root:  pwsh installer/build-wxs.ps1
# Output: installer/Package.wxs (committed; CI builds it directly).

$ErrorActionPreference = 'Stop'

# Languages in display order. Code is the WIXUI_LANG value; Name is the native label.
$langs = @(
    @{ code = 'en'; name = 'English' }
    @{ code = 'ja'; name = '日本語' }
    @{ code = 'zh'; name = '中文（简体）' }
    @{ code = 'es'; name = 'Español' }
    @{ code = 'de'; name = 'Deutsch' }
    @{ code = 'fr'; name = 'Français' }
    @{ code = 'ko'; name = '한국어' }
)

# key -> per-language text. English is also the Property default (fallback).
$S = [ordered]@{
    welcome_title = @{
        en='Welcome to MidiToEverything Setup'; ja='MidiToEverything セットアップへようこそ'
        zh='欢迎使用 MidiToEverything 安装程序'; es='Bienvenido al instalador de MidiToEverything'
        de='Willkommen beim MidiToEverything-Setup'; fr="Bienvenue dans l'installation de MidiToEverything"
        ko='MidiToEverything 설치를 시작합니다' }
    welcome_body = @{
        en='Setup will install MidiToEverything on your computer. Click Next to continue or Cancel to exit.'
        ja='MidiToEverything をこのコンピューターにインストールします。続行するには「次へ」、終了するには「キャンセル」をクリックしてください。'
        zh='安装程序将在您的计算机上安装 MidiToEverything。点击“下一步”继续，或点击“取消”退出。'
        es='El instalador instalará MidiToEverything en su equipo. Haga clic en Siguiente para continuar o en Cancelar para salir.'
        de='Das Setup installiert MidiToEverything auf Ihrem Computer. Klicken Sie auf Weiter, um fortzufahren, oder auf Abbrechen, um zu beenden.'
        fr="L'installation va installer MidiToEverything sur votre ordinateur. Cliquez sur Suivant pour continuer ou sur Annuler pour quitter."
        ko='이 설치 프로그램은 MidiToEverything 을(를) 컴퓨터에 설치합니다. 계속하려면 [다음], 종료하려면 [취소] 를 클릭하세요.' }
    dir_title = @{
        en='Destination Folder'; ja='インストール先フォルダー'; zh='目标文件夹'; es='Carpeta de destino'
        de='Zielordner'; fr='Dossier de destination'; ko='설치 폴더' }
    dir_desc = @{
        en='Choose where MidiToEverything will be installed.'; ja='MidiToEverything のインストール先を選択してください。'
        zh='选择 MidiToEverything 的安装位置。'; es='Elija dónde se instalará MidiToEverything.'
        de='Wählen Sie, wohin MidiToEverything installiert wird.'; fr="Choisissez l'emplacement d'installation de MidiToEverything."
        ko='MidiToEverything 을(를) 설치할 위치를 선택하세요.' }
    dir_label = @{
        en='Install MidiToEverything to:'; ja='インストール先:'; zh='安装到:'; es='Instalar en:'
        de='Installieren nach:'; fr='Installer dans :'; ko='설치 위치:' }
    btn_change = @{
        en='Change...'; ja='変更...'; zh='更改...'; es='Cambiar...'; de='Ändern...'; fr='Modifier...'; ko='변경...' }
    opt_title = @{
        en='Additional Options'; ja='追加オプション'; zh='其他选项'; es='Opciones adicionales'
        de='Zusätzliche Optionen'; fr='Options supplémentaires'; ko='추가 옵션' }
    opt_desc = @{
        en='Choose what Setup should do.'; ja='セットアップで行う操作を選択してください。'; zh='选择安装程序要执行的操作。'
        es='Elija qué debe hacer el instalador.'; de='Wählen Sie, was das Setup tun soll.'
        fr='Choisissez ce que le programme doit faire.'; ko='설치 프로그램이 수행할 작업을 선택하세요.' }
    opt_desktop = @{
        en='Create a shortcut on the Desktop'; ja='デスクトップにショートカットを作成する'; zh='在桌面上创建快捷方式'
        es='Crear un acceso directo en el Escritorio'; de='Eine Verknüpfung auf dem Desktop erstellen'
        fr='Créer un raccourci sur le Bureau'; ko='바탕 화면에 바로 가기 만들기' }
    opt_startup = @{
        en='Start MidiToEverything when Windows starts'; ja='Windows 起動時に MidiToEverything を起動する'
        zh='Windows 启动时自动运行 MidiToEverything'; es='Iniciar MidiToEverything al arrancar Windows'
        de='MidiToEverything beim Windows-Start ausführen'; fr='Lancer MidiToEverything au démarrage de Windows'
        ko='Windows 시작 시 MidiToEverything 실행' }
    ready_title = @{
        en='Ready to Install'; ja='インストールの準備完了'; zh='准备安装'; es='Listo para instalar'
        de='Bereit zur Installation'; fr='Prêt à installer'; ko='설치 준비 완료' }
    ready_body = @{
        en='Click Install to begin. Click Back to review or change your settings.'
        ja='「インストール」をクリックすると開始します。設定を確認・変更するには「戻る」をクリックしてください。'
        zh='点击“安装”开始。点击“上一步”可查看或更改设置。'
        es='Haga clic en Instalar para comenzar. Haga clic en Atrás para revisar o cambiar la configuración.'
        de='Klicken Sie auf Installieren, um zu beginnen. Klicken Sie auf Zurück, um Einstellungen zu prüfen oder zu ändern.'
        fr='Cliquez sur Installer pour commencer. Cliquez sur Précédent pour revoir ou modifier les paramètres.'
        ko='[설치] 를 클릭하면 시작됩니다. 설정을 확인/변경하려면 [뒤로] 를 클릭하세요.' }
    exit_title = @{
        en='Installation Complete'; ja='インストール完了'; zh='安装完成'; es='Instalación completada'
        de='Installation abgeschlossen'; fr='Installation terminée'; ko='설치 완료' }
    exit_body = @{
        en='MidiToEverything has been installed successfully.'; ja='MidiToEverything のインストールが完了しました。'
        zh='MidiToEverything 已成功安装。'; es='MidiToEverything se ha instalado correctamente.'
        de='MidiToEverything wurde erfolgreich installiert.'; fr='MidiToEverything a été installé avec succès.'
        ko='MidiToEverything 이(가) 성공적으로 설치되었습니다.' }
    opt_launch = @{
        en='Launch MidiToEverything now'; ja='今すぐ MidiToEverything を起動する'; zh='立即启动 MidiToEverything'
        es='Iniciar MidiToEverything ahora'; de='MidiToEverything jetzt starten'
        fr='Lancer MidiToEverything maintenant'; ko='지금 MidiToEverything 실행' }
    btn_back = @{ en='Back'; ja='戻る'; zh='上一步'; es='Atrás'; de='Zurück'; fr='Précédent'; ko='뒤로' }
    btn_next = @{ en='Next'; ja='次へ'; zh='下一步'; es='Siguiente'; de='Weiter'; fr='Suivant'; ko='다음' }
    btn_cancel = @{ en='Cancel'; ja='キャンセル'; zh='取消'; es='Cancelar'; de='Abbrechen'; fr='Annuler'; ko='취소' }
    btn_install = @{ en='Install'; ja='インストール'; zh='安装'; es='Instalar'; de='Installieren'; fr='Installer'; ko='설치' }
    btn_finish = @{ en='Finish'; ja='完了'; zh='完成'; es='Finalizar'; de='Fertig stellen'; fr='Terminer'; ko='마침' }
}

function Esc([string]$s) { [System.Security.SecurityElement]::Escape($s) }

# --- Property defaults (English fallback) ---
$propDefaults = ($S.Keys | ForEach-Object {
    "    <Property Id=`"S_$_`" Value=`"$(Esc $S[$_].en)`" />"
}) -join "`n"

# --- LangDlg combo entries ---
$comboItems = ($langs | ForEach-Object {
    "        <ListItem Value=`"$($_.code)`" Text=`"$(Esc $_.name)`" />"
}) -join "`n"

# --- SetProperty cascade: on LangDlg Next, set every string for the chosen lang ---
$cascade = foreach ($l in $langs) {
    foreach ($k in $S.Keys) {
        $cond = "WIXUI_LANG = &quot;$($l.code)&quot;"
        "          <Publish Property=`"S_$k`" Value=`"$(Esc $S[$k][$l.code])`" Order=`"1`" Condition=`"$cond`" />"
    }
}
$cascade = $cascade -join "`n"

$tpl = @"
<?xml version="1.0" encoding="utf-8"?>
<!--
  GENERATED by installer/build-wxs.ps1 — do not edit by hand; edit the script.
  Per-machine MSI with a custom multilingual wizard (language chosen on the first
  page). Built by the Release workflow via "wix build" with the UI + Util extensions.
-->
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <Package Name="MidiToEverything"
           Manufacturer="guy7cc"
           Version="`$(var.Version)"
           UpgradeCode="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C20"
           Scope="perMachine"
           Codepage="65001"
           Compressed="yes">

    <MajorUpgrade DowngradeErrorMessage="A newer version of MidiToEverything is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Icon Id="AppIcon" SourceFile="src\App\app.ico" />
    <Property Id="ARPPRODUCTICON" Value="AppIcon" />
    <Property Id="ARPURLINFOABOUT" Value="https://github.com/guy7cc/MidiToEverything" />

    <!-- Wizard options. Desktop shortcut on by default; startup off (left undeclared). -->
    <Property Id="INSTALLDESKTOPSHORTCUT" Value="1" Secure="yes" />
    <Property Id="WIXUI_LANG" Value="en" Secure="yes" />
    <Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />

    <!-- Localized UI strings (English defaults; LangDlg overrides per language). -->
$propDefaults

    <Feature Id="Main" Title="MidiToEverything" Level="1">
      <ComponentRef Id="MainExe" />
      <ComponentGroupRef Id="AppFiles" />
      <ComponentGroupRef Id="ExtraFiles" />
      <ComponentRef Id="PluginsDir" />
      <ComponentRef Id="StartMenuShortcut" />
      <ComponentRef Id="DesktopShortcut" />
      <ComponentRef Id="StartupRun" />
      <ComponentRef Id="InstallDirReg" />
    </Feature>

    <!-- Remember the install location across upgrades: a prior install records the folder (see the
         InstallDirReg component), and this search restores it so a silent MajorUpgrade reinstalls in
         place instead of reverting to the default Program Files folder. -->
    <Property Id="INSTALLFOLDER">
      <RegistrySearch Id="PrevInstallFolder" Root="HKLM" Key="Software\guy7cc\MidiToEverything" Name="InstallDir" Type="raw" />
    </Property>

    <!-- WIXUI_INSTALLDIR holds the directory ID; the folder field edits it indirectly. -->
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />
    <Property Id="WixShellExecTarget" Value="[#MidiToEverythingExe]" />
    <CustomAction Id="LaunchApplication" BinaryRef="Wix4UtilCA_X64" DllEntry="WixShellExec" Impersonate="yes" />

    <UI Id="WizardUI">
      <TextStyle Id="WixUIFont_Normal" FaceName="Tahoma" Size="8" />
      <TextStyle Id="WixUIFont_Bigger" FaceName="Tahoma" Size="12" />
      <TextStyle Id="WixUIFont_Title" FaceName="Tahoma" Size="9" Bold="yes" />
      <Property Id="DefaultUIFont" Value="WixUIFont_Normal" />

      <!-- Reuse the proven built-in dialogs for browse / progress / errors / maintenance. -->
      <DialogRef Id="BrowseDlg" />
      <DialogRef Id="DiskCostDlg" />
      <DialogRef Id="ErrorDlg" />
      <DialogRef Id="FatalError" />
      <DialogRef Id="FilesInUse" />
      <DialogRef Id="MsiRMFilesInUse" />
      <DialogRef Id="PrepareDlg" />
      <DialogRef Id="ProgressDlg" />
      <DialogRef Id="ResumeDlg" />
      <DialogRef Id="UserExit" />
      <DialogRef Id="CancelDlg" />
      <DialogRef Id="MaintenanceWelcomeDlg" />
      <DialogRef Id="MaintenanceTypeDlg" />
      <DialogRef Id="VerifyReadyDlg" />

      <!-- ===== LangDlg (first page; static, language-neutral) ===== -->
      <Dialog Id="LangDlg" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}Select language / 言語の選択" />
        <Control Id="Desc" Type="Text" X="25" Y="23" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="Choose the language for this installation." />
        <Control Id="LangLabel" Type="Text" X="20" Y="70" Width="100" Height="15" NoPrefix="yes" Text="Language:" />
        <Control Id="LangCombo" Type="ComboBox" X="20" Y="86" Width="200" Height="80" Property="WIXUI_LANG" ComboList="yes" Sorted="no">
          <ComboBox Property="WIXUI_LANG">
$comboItems
          </ComboBox>
        </Control>
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Next" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Text="Next">
$cascade
          <Publish Event="NewDialog" Value="MyWelcomeDlg" Order="50" />
        </Control>
        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes" Text="Cancel">
          <Publish Event="SpawnDialog" Value="CancelDlg" />
        </Control>
      </Dialog>

      <!-- ===== WelcomeDlg ===== -->
      <Dialog Id="MyWelcomeDlg" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}[S_welcome_title]" />
        <Control Id="Body" Type="Text" X="25" Y="70" Width="320" Height="120" NoPrefix="yes" Text="[S_welcome_body]" />
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Back" Type="PushButton" X="180" Y="243" Width="56" Height="17" Text="[S_btn_back]">
          <Publish Event="NewDialog" Value="LangDlg" />
        </Control>
        <Control Id="Next" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Text="[S_btn_next]">
          <Publish Event="NewDialog" Value="MyInstallDirDlg" />
        </Control>
        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes" Text="[S_btn_cancel]">
          <Publish Event="SpawnDialog" Value="CancelDlg" />
        </Control>
      </Dialog>

      <!-- ===== InstallDirDlg ===== -->
      <Dialog Id="MyInstallDirDlg" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}[S_dir_title]" />
        <Control Id="Desc" Type="Text" X="25" Y="23" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="[S_dir_desc]" />
        <Control Id="FolderLabel" Type="Text" X="20" Y="75" Width="320" Height="15" NoPrefix="yes" Text="[S_dir_label]" />
        <Control Id="Folder" Type="PathEdit" X="20" Y="92" Width="250" Height="18" Property="WIXUI_INSTALLDIR" Indirect="yes" />
        <Control Id="ChangeFolder" Type="PushButton" X="276" Y="91" Width="64" Height="19" Text="[S_btn_change]">
          <Publish Property="_BrowseProperty" Value="[WIXUI_INSTALLDIR]" Order="1" />
          <Publish Event="SpawnDialog" Value="BrowseDlg" Order="2" />
        </Control>
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Back" Type="PushButton" X="180" Y="243" Width="56" Height="17" Text="[S_btn_back]">
          <Publish Event="NewDialog" Value="MyWelcomeDlg" />
        </Control>
        <Control Id="Next" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Text="[S_btn_next]">
          <Publish Event="SetTargetPath" Value="[WIXUI_INSTALLDIR]" Order="1" />
          <Publish Event="NewDialog" Value="OptionsDlg" Order="2" />
        </Control>
        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes" Text="[S_btn_cancel]">
          <Publish Event="SpawnDialog" Value="CancelDlg" />
        </Control>
      </Dialog>

      <!-- ===== OptionsDlg ===== -->
      <Dialog Id="OptionsDlg" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}[S_opt_title]" />
        <Control Id="Desc" Type="Text" X="25" Y="23" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="[S_opt_desc]" />
        <Control Id="DesktopCheck" Type="CheckBox" X="20" Y="70" Width="330" Height="18" Property="INSTALLDESKTOPSHORTCUT" CheckBoxValue="1" Text="[S_opt_desktop]" />
        <Control Id="StartupCheck" Type="CheckBox" X="20" Y="96" Width="330" Height="18" Property="INSTALLSTARTUP" CheckBoxValue="1" Text="[S_opt_startup]" />
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Back" Type="PushButton" X="180" Y="243" Width="56" Height="17" Text="[S_btn_back]">
          <Publish Event="NewDialog" Value="MyInstallDirDlg" />
        </Control>
        <Control Id="Next" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Text="[S_btn_next]">
          <Publish Event="NewDialog" Value="VerifyReadyDlg2" />
        </Control>
        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes" Text="[S_btn_cancel]">
          <Publish Event="SpawnDialog" Value="CancelDlg" />
        </Control>
      </Dialog>

      <!-- ===== VerifyReadyDlg2 (custom localized confirm) ===== -->
      <Dialog Id="VerifyReadyDlg2" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}[S_ready_title]" />
        <Control Id="Body" Type="Text" X="25" Y="70" Width="320" Height="80" NoPrefix="yes" Text="[S_ready_body]" />
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Back" Type="PushButton" X="180" Y="243" Width="56" Height="17" Text="[S_btn_back]">
          <Publish Event="NewDialog" Value="OptionsDlg" />
        </Control>
        <Control Id="Install" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Text="[S_btn_install]">
          <Publish Event="EndDialog" Value="Return" />
        </Control>
        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes" Text="[S_btn_cancel]">
          <Publish Event="SpawnDialog" Value="CancelDlg" />
        </Control>
      </Dialog>

      <!-- ===== ExitDlg2 (custom localized finish + launch checkbox) ===== -->
      <Dialog Id="ExitDlg2" Width="370" Height="270" Title="MidiToEverything Setup">
        <Control Id="Banner" Type="Bitmap" X="0" Y="0" Width="370" Height="44" TabSkip="no" Text="WixUI_Bmp_Banner" />
        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0" />
        <Control Id="Title" Type="Text" X="15" Y="6" Width="340" Height="15" Transparent="yes" NoPrefix="yes" Text="{\WixUIFont_Title}[S_exit_title]" />
        <Control Id="Body" Type="Text" X="25" Y="70" Width="320" Height="40" NoPrefix="yes" Text="[S_exit_body]" />
        <Control Id="LaunchCheck" Type="CheckBox" X="25" Y="120" Width="320" Height="18" Property="WIXUI_EXITDIALOGOPTIONALCHECKBOX" CheckBoxValue="1" Text="[S_opt_launch]" />
        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0" />
        <Control Id="Finish" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes" Cancel="yes" Text="[S_btn_finish]">
          <Publish Event="DoAction" Value="LaunchApplication" Order="1" Condition="WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1 and NOT Installed" />
          <Publish Event="EndDialog" Value="Return" Order="2" />
        </Control>
      </Dialog>

      <!-- PrepareDlg/ProgressDlg/ResumeDlg/MaintenanceWelcomeDlg are scheduled by the
           referenced wixlib dialogs; only the install entry (LangDlg) and the success
           page (custom ExitDlg2) need to be added here. -->
      <InstallUISequence>
        <Show Dialog="LangDlg" Before="ProgressDlg" Condition="NOT Installed" />
        <Show Dialog="ExitDlg2" OnExit="success" />
      </InstallUISequence>
    </UI>
  </Package>

  <Fragment>
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="MidiToEverything">
        <Directory Id="PluginsFolder" Name="plugins" />
      </Directory>
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder" />
    <StandardDirectory Id="DesktopFolder" />
  </Fragment>

  <Fragment>
    <Component Id="MainExe" Directory="INSTALLFOLDER" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C25">
      <File Id="MidiToEverythingExe" Source="`$(var.PublishDir)\MidiToEverything.exe" KeyPath="yes" />
    </Component>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
      <Files Include="`$(var.PublishDir)\**">
        <Exclude Files="`$(var.PublishDir)\MidiToEverything.exe" />
        <Exclude Files="`$(var.PublishDir)\**\*.pdb" />
        <Exclude Files="`$(var.PublishDir)\**\*.dylib" />
        <Exclude Files="`$(var.PublishDir)\**\*.so" />
      </Files>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ExtraFiles" Directory="INSTALLFOLDER">
      <Component>
        <File Source="README.md" />
      </Component>
      <Component>
        <File Source="samples\config.sample.json" />
      </Component>
    </ComponentGroup>
  </Fragment>

  <Fragment>
    <Component Id="PluginsDir" Directory="PluginsFolder" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C21">
      <CreateFolder />
    </Component>
  </Fragment>

  <Fragment>
    <Component Id="StartMenuShortcut" Directory="ProgramMenuFolder" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C22">
      <Shortcut Id="StartShortcut" Name="MidiToEverything" Target="[#MidiToEverythingExe]" WorkingDirectory="INSTALLFOLDER" Icon="AppIcon" />
      <RegistryValue Root="HKMU" Key="Software\guy7cc\MidiToEverything" Name="installed" Type="integer" Value="1" KeyPath="yes" />
    </Component>
  </Fragment>

  <Fragment>
    <Component Id="DesktopShortcut" Directory="DesktopFolder" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C23" Condition="INSTALLDESKTOPSHORTCUT = &quot;1&quot;">
      <Shortcut Id="DesktopSc" Name="MidiToEverything" Target="[#MidiToEverythingExe]" WorkingDirectory="INSTALLFOLDER" Icon="AppIcon" />
      <RegistryValue Root="HKMU" Key="Software\guy7cc\MidiToEverything" Name="desktopShortcut" Type="integer" Value="1" KeyPath="yes" />
    </Component>
  </Fragment>

  <Fragment>
    <Component Id="StartupRun" Directory="INSTALLFOLDER" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C24" Condition="INSTALLSTARTUP = &quot;1&quot;">
      <RegistryValue Root="HKLM" Key="Software\Microsoft\Windows\CurrentVersion\Run" Name="MidiToEverything" Type="string" Value="&quot;[#MidiToEverythingExe]&quot;" KeyPath="yes" />
    </Component>
  </Fragment>

  <Fragment>
    <!-- Record the chosen install folder so the next (silent) upgrade can restore it. -->
    <Component Id="InstallDirReg" Directory="INSTALLFOLDER" Guid="6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C26">
      <RegistryValue Root="HKLM" Key="Software\guy7cc\MidiToEverything" Name="InstallDir" Type="string" Value="[INSTALLFOLDER]" KeyPath="yes" />
    </Component>
  </Fragment>

</Wix>
"@

$out = Join-Path $PSScriptRoot 'Package.wxs'
$tpl | Set-Content -Path $out -Encoding UTF8
Write-Host "Wrote $out"
