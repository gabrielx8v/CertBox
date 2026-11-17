# Dependências e fontes correspondentes

Desenvolvido com IA. Este inventário descreve os componentes externos; eles
mantêm a autoria e as licenças de seus projetos originais.

Referência das entregas: Windows x64 e Linux x64, com versões fixadas em
packages.windows.lock.json, packages.linux.lock.json e no projeto. DEPENDENCIES.json contém as revisões e os endereços para obtenção das
fontes. Os avisos originais recebidos são preservados em licenses/ e incorporados
ao executável. Não substituir esses avisos pela GPL do código próprio.

| Componente | Versão | Licença principal | Fonte da revisão usada |
| --- | --- | --- | --- |
| Avalonia, Win32/X11/FreeDesktop, Skia, Fluent, Remote.Protocol e pacote da fonte | 11.3.23 | MIT; fonte Inter sob OFL 1.1 | [Avalonia](https://github.com/AvaloniaUI/Avalonia/tree/03bd7e694d8ae1247663b8a9a36f536f785c2bd3) |
| SkiaSharp e nativos Windows/Linux | 2.88.9 | MIT e avisos nativos | [SkiaSharp](https://github.com/mono/SkiaSharp/tree/9699cdf046a989423a5e5eca68e7fd813486d81c) |
| HarfBuzzSharp e nativos Windows/Linux | 8.3.1.1 | MIT e avisos nativos | [HarfBuzzSharp](https://github.com/mono/SkiaSharp/tree/64f24b1cddb68d30ec0ac6b661964178fc21d5ec) |
| ANGLE para Avalonia | 2.1.25547.20250602 | BSD e avisos de componentes internos | [ANGLE](https://github.com/AvaloniaUI/angle/tree/cb8b4e1307a9d8f5ff56b8c5973bea4158ffead8) |
| MicroCom.Runtime | 0.11.0 | MIT | [MicroCom](https://github.com/kekekeks/MicroCom/tree/7e773759cfc1012a5d080d4129c4718a662884b0) |
| Runtime .NET Windows/Linux | 10.0.15 | MIT e avisos internos | [.NET](https://github.com/dotnet/dotnet/tree/8e0f8ed2312954727f8919d7d1c2f78b483b4ee7) |

Linux também inclui Avalonia.X11 e Avalonia.FreeDesktop sob MIT, na mesma revisão
do Avalonia, e Tmds.DBus.Protocol 0.21.3 (MIT, Tom Deseyn), revisão
[8cb04f66c330b64244e996ff68f685f27f7381a0](https://github.com/tmds/Tmds.DBus/tree/8cb04f66c330b64244e996ff68f685f27f7381a0).
Os textos Linux de SkiaSharp, HarfBuzzSharp e .NET são incorporados somente no
pacote Linux; o Windows mantém seus próprios avisos. Bibliotecas do sistema Linux
não são distribuídas no TAR.GZ. A GPL-3.0-or-later do aplicativo permanece a mesma.

Avalonia.BuildServices e ILLink são ferramentas de compilação, não programas
oferecidos ao usuário. As respectivas revisões também estão no inventário JSON.

# Obter as fontes

As revisões acima são acessíveis sem executar o CertBox. Para obter uma cópia
local dos repositórios e seus submódulos, utilize scripts/fetch-dependency-sources.ps1.
Esse comando precisa de Git, acesso à rede e espaço em disco; não é executado
pelo programa nem pelo build normal. Os projetos de runtime e gráficos podem
ser grandes. As instruções de compilação de cada biblioteca estão em seu próprio
repositório, na revisão indicada.

Para SkiaSharp, o submódulo Skia é
4bed689c9c9eb77a120c6a9d54af6a572c85d1c2. Para HarfBuzzSharp, o submódulo Skia é
30abf4a0dcd3006f3a555e46906151e486f87b6e. As dependências obtidas pelo mecanismo
DEPS do Skia/ANGLE seguem as revisões indicadas nesses arquivos; os scripts
upstream explicam como sincronizá-las.

As revisões dos pacotes foram lidas dos metadados NuGet. O pacote MicroCom
0.11.0 não grava o commit no nuspec: a revisão indicada foi identificada pelo
commit que define Version=0.11.0 e a assinatura de assembly, anterior à publicação
do pacote. Essa identificação é evidência de procedência, não comparação de
uma recompilação bit a bit desse pacote.

# Conferência dos avisos nativos

Os arquivos THIRD-PARTY-NOTICES de SkiaSharp/HarfBuzzSharp são agregados e não
significam que cada biblioteca citada foi ligada à entrega Windows.

- O build Windows do SkiaSharp utiliza o alvo SkiaSharp, com ferramentas
  desativadas, ICU e sfntly desativados. O libmicrohttpd listado nos avisos é
  dependência de skiaserve, uma ferramenta de teste, e não desse alvo de biblioteca.
  Evidências: [build Windows](https://github.com/mono/SkiaSharp/blob/9699cdf046a989423a5e5eca68e7fd813486d81c/native/windows/build.cake),
  [opções compartilhadas](https://github.com/mono/SkiaSharp/blob/9699cdf046a989423a5e5eca68e7fd813486d81c/cake/native-shared.cake)
  e [alvos Skia](https://github.com/mono/skia/blob/4bed689c9c9eb77a120c6a9d54af6a572c85d1c2/BUILD.gn).
- O projeto nativo HarfBuzzSharp compila harfbuzz-subset.cc da revisão fixada.
  [Projeto Windows](https://github.com/mono/SkiaSharp/blob/64f24b1cddb68d30ec0ac6b661964178fc21d5ec/native/windows/libHarfBuzzSharp/libHarfBuzzSharp.vcxproj).
- A pipeline da versão ANGLE consultada desativa Vulkan, OpenGL e o backend
  nulo. O pacote Windows x64 inclui av_libglesv2.dll.
  [Pipeline](https://github.com/AvaloniaUI/angle/blob/cb8b4e1307a9d8f5ff56b8c5973bea4158ffead8/azure-pipelines.yml).
- Apache 2.0 é compatível com GPLv3 e a FTL do FreeType declara compatibilidade
  com GPLv3. A fonte Inter acompanha o aplicativo sob OFL, preservando sua licença.
- A lista geral do runtime .NET também contém avisos de outras plataformas.
  A menção a um componente nessa lista não demonstra sua inclusão no Windows.
  A cláusula publicitária histórica de Berkeley foi removida pela universidade;
  não confundir essa situação com cláusulas de outros autores.
  [Contexto da FSF](https://www.gnu.org/licenses/bsd.html).

Os textos originais foram mantidos por inteiro, inclusive avisos agregados.
Esta conferência verifica metadados, fontes e configurações upstream; não
representa certificação jurídica ou uma reconstrução de cada binário externo.

# Disponibilidade na release

A release deve apontar para o código próprio da mesma tag, suas instruções de
build e este inventário de fontes. Preserve a disponibilidade dos endereços ou
forneça acesso equivalente se uma origem externa deixar de existir. Um hash
de pacote não substitui o acesso ao seu código-fonte.
