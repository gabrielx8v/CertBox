# Dependências e fontes correspondentes

Desenvolvido com IA. Componentes externos mantêm a autoria e as licenças dos
projetos originais. Referência: entregas Windows x64 e Linux x64 do CertBox 2.3.1.

As versões resolvidas estão em packages.windows.lock.json e packages.linux.lock.json.
Todos os pacotes Avalonia usam a propriedade AvaloniaVersion do projeto, inclusive
os backends condicionais Win32 e X11. DEPENDENCIES.json registra pacotes, revisões
upstream e os endereços para obter as fontes correspondentes.

| Componente | Versão | Licença principal | Fonte da revisão |
| --- | --- | --- | --- |
| Avalonia, Win32/X11/FreeDesktop/AtSpi, Skia, HarfBuzz, Fluent e fonte | 12.1.6 | MIT; Inter sob OFL 1.1 | [Avalonia](https://github.com/AvaloniaUI/Avalonia/tree/cf8e564674700348947e93ab19105f27afe859a9) |
| SkiaSharp e nativos Windows/Linux | 3.119.4 | MIT e avisos nativos | [SkiaSharp](https://github.com/mono/SkiaSharp/tree/f568ac94dd768ef9a2f593537cfde2dd0d348ef5) |
| HarfBuzzSharp e nativos Windows/Linux | 8.3.1.3 | MIT e avisos nativos | [HarfBuzzSharp](https://github.com/mono/SkiaSharp/tree/2888c737ad016d584c74525e2d35db5097ea8576) |
| ANGLE, somente Windows | 2.1.27548.20260419 | BSD e avisos internos | [ANGLE](https://github.com/AvaloniaUI/angle/tree/1c89805903c1482166356d3b950d474973180e61) |
| MicroCom.Runtime | 0.11.6 | MIT | [MicroCom](https://github.com/kekekeks/MicroCom/tree/76785efcafd91b5902fd19dd11145f6dd655b7b4) |
| Tmds.DBus.Protocol, somente Linux | 0.94.1 | MIT | [Tmds.DBus](https://github.com/tmds/Tmds.DBus/tree/b4a7fed0b878f74cb54f7cca84d2889af4e596ba) |
| Runtime .NET Windows/Linux | 10.0.15 | MIT e avisos internos | [.NET](https://github.com/dotnet/dotnet/tree/8e0f8ed2312954727f8919d7d1c2f78b483b4ee7) |

O SDK de compilação foi atualizado para 11.0.100. O aplicativo continua em
net10.0 e o runtime distribuído permanece 10.0.15. O SDK, Avalonia.BuildServices
11.3.2 e Microsoft.NET.ILLink.Tasks 10.0.15 são ferramentas de compilação;
suas revisões também estão no inventário JSON. O SDK completo não é distribuído
no executável. As versões efetivamente restauradas constam dos lockfiles.

## Avisos e procedência

Os avisos originais estão em licenses/ e são incorporados na distribuição da
plataforma correspondente, acessíveis pelas configurações do programa. Os textos
de SkiaSharp/HarfBuzzSharp e do runtime foram copiados dos pacotes NuGet usados.
As licenças MIT de Avalonia, MicroCom e Tmds foram obtidas nas revisões registradas.
Não substitua esses avisos pela GPL do código próprio do CertBox.

Os arquivos THIRD-PARTY-NOTICES são agregados upstream e podem mencionar
componentes opcionais ou de outras plataformas. Sua presença no texto não prova
que cada biblioteca foi ligada à entrega. Preserve os textos integralmente.
As bibliotecas do sistema Linux descritas em [LINUX.md](docs/LINUX.md), como
OpenSSL, X11 e fontconfig, não são incluídas no TAR.GZ.

As revisões de pacotes foram lidas dos nuspecs. O commit do SDK foi lido de
sdk/11.0.100/.version no arquivo oficial, verificado pelo SHA-512 dos metadados
de release da Microsoft. Isso registra a procedência; não representa comparação
de uma reconstrução bit a bit de todos os binários externos ou certificação jurídica.

## Obter as fontes

Use scripts/fetch-dependency-sources.ps1 para obter os repositórios dos componentes
e seus submódulos nas revisões do inventário. O comando requer Git, rede e espaço
em disco e não é executado pelo programa ou pelo build normal. As instruções de
compilação de cada componente estão no respectivo repositório e revisão.

SkiaSharp utiliza o submódulo Skia 7dbfc07dd33181f84e0958afb7ee805c6c769f0b;
HarfBuzzSharp utiliza 927041a58f130e0dd0562ba86cb4170989ad39e9. Componentes
obtidos por DEPS seguem as revisões registradas nesses arquivos upstream.

A release deve apontar para o fonte do CertBox na mesma tag, suas instruções de
build e este inventário. Preserve os endereços ou forneça acesso equivalente às
fontes se uma origem deixar de existir. Um hash não substitui acesso ao fonte.
