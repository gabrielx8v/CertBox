# Compilação e entrega

Desenvolvido com IA. O processo abaixo mantém testes e publicação reproduzíveis
a partir do código e das versões de pacotes fixadas, sem prometer identidade
binária entre SDKs, ambientes e versões diferentes.

Para o script principal: Windows x64, PowerShell e .NET SDK 10.0.404, fixado em global.json.
A primeira restauração precisa de acesso ao NuGet. Git é necessário apenas
para obter o projeto ou as fontes das dependências. O usuário final recebe um
pacote com runtime incluído e não precisa instalar .NET ou ferramentas de build.
No Windows, OpenSSL não precisa ser instalado. No Linux, as bibliotecas do
sistema descritas em docs/LINUX.md são necessárias.

Execute a partir da pasta que contém este arquivo:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

O script executa os testes do núcleo e da interface sem abrir janelas, publica
Windows x64 e Linux x64 com trimming e compressão, verifica versões e grava
os hashes. O Windows usa somente Win32; Linux usa somente X11/FreeDesktop.
Não são gerados programas CLI, iniciadores shell, instaladores ou ZIPs de código.
As entregas são certbox-windows-x64.exe e certbox-linux-x64.tar.gz. O TAR.GZ
contém o executável gráfico Linux com permissão de execução, ícone e instruções.

No ambiente original, o código fica em source e o executável em ../output.
Se o conteúdo de source for a raiz de um clone público com outro nome, o script
utiliza output dentro desse clone. A opção -OutputDirectory permite escolher
outro destino explicitamente. -Target Windows ou -Target Linux seleciona uma
compilação; -ForRelease exige Both. work, bin e obj são intermediários ignorados
pelo Git. Objetos e lockfiles são separados por plataforma para evitar que a
restauração de uma altere as dependências da outra.

A versão Linux não foi testada manualmente. Ela usa X11 ou XWayland; consulte
docs/LINUX.md para requisitos e limitações. Uma compilação direta com SDK Linux:

```text
dotnet publish src/CertBox/CertBox.csproj -c Release -r linux-x64 -p:CertBoxPlatform=linux --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o work/publish/certbox-linux
```

# Endereço único do projeto

Edite somente repositoryUrl em project.json, com o endereço HTTPS do repositório
GitHub, sem /issues, /tree, parâmetros ou fragmentos. Depois compile novamente.
O build atualiza os blocos de links dos três READMEs e incorpora a configuração
ao programa. Os links do aplicativo ficam ocultos enquanto o endereço está vazio.

Use -ForRelease para exigir um endereço válido e gerar as notas públicas de
entrega com links para o código da tag, suporte, privacidade e hash, além de uma
pasta limpa em work/github-current. Criar o
repositório, a tag e a publicação no GitHub continua sendo uma ação separada.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -ForRelease
```

# Verificação adicional do runtime reduzido

O script scripts/test-published.ps1 testa uma publicação sem bundle usando as
próprias DLLs e o próprio CoreCLR reduzidos. Ele não faz parte da aplicação e
não abre janelas. Compare seus assemblies com as entradas do linker da versão
de arquivo único antes de tratar o resultado como evidência dessa mesma entrega.

```powershell
dotnet publish src/CertBox/CertBox.csproj -c Release -r win-x64 -p:CertBoxPlatform=windows --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o work/publish/verification-windows
powershell -ExecutionPolicy Bypass -File scripts/test-published.ps1 -PublishedDirectory work/publish/verification-windows
```

Para Linux, publique com -r linux-x64 -p:CertBoxPlatform=linux e use
test-published.ps1 -RuntimeIdentifier linux-x64 -WslDistribution Debian, apontando
para a publicação Linux sem bundle. Esse modo precisa de WSL e bibliotecas
gráficas disponíveis; não instala pacotes automaticamente nem abre janelas.

# Publicação do código correspondente

Publique o conteúdo desta pasta como raiz do repositório público, incluindo
LICENSE, NOTICE.md, src, tests, scripts, tools, docs, licenses, project.json, global.json,
NuGet.Config, os dois lockfiles e as referências das dependências. Não publique work,
bin, obj, entradas de teste geradas, preferências pessoais ou chaves reais.

Use a pasta repository produzida em work/github-current como origem do primeiro
envio ao GitHub. Ela contém apenas os arquivos públicos atuais. A pasta
release-assets separa as duas entregas e seus hashes para a release. Históricos locais
e anotações internas são excluídos desse preparo; não é criado histórico Git
anterior nem um ZIP de código.

A tag v2.3.0 deve apontar para o código usado na compilação 2.3.0. Cada release
deve permitir obter seu código correspondente e suas instruções de build.
Não substitua silenciosamente o EXE de uma release já publicada: crie uma nova
versão e um novo hash. Veja RELEASE.md e DEPENDENCIES.md.
