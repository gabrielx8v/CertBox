# Entrega direta pelo GitHub

Desenvolvido com IA. A entrega direta inclui um EXE Windows x64 portátil, sem
assinatura Authenticode, e um TAR.GZ com a aplicação gráfica Linux x64. Ambos
acompanham hashes SHA-256 e o código da mesma versão no repositório. A preparação
específica de Microsoft Store/MSIX é posterior.

**A versão Linux não foi testada manualmente.** Essa observação deve permanecer
nos três READMEs e nas notas da release, mesmo quando testes automatizados passarem.

Antes da primeira publicação:

1. Confira a URL oficial em project.json e compile com scripts/build.ps1 -ForRelease.
2. Publique work/github-current/repository como raiz do repositório, sem histórico anterior.
3. Crie a tag v2.3.1 no código usado no build e uma release correspondente.
4. Anexe o EXE, o TAR.GZ Linux e SHA256SUMS.txt de work/github-current/release-assets.
5. Use o texto gerado em release/RELEASE_NOTES.md e mantenha o código da tag disponível.
6. Confira manualmente a interface e os diálogos em Windows x64, inclusive
   escala de tela, teclado, importação, exportação e troca de idioma.

O TAR.GZ preserva a permissão de execução do binário Linux e não inclui shell
launcher nem versão CLI. Requisitos: docs/LINUX.md.

O script prepara arquivos locais e não publica nem envia mensagens ao GitHub.
Ele não assina executáveis, não cria contas e não instala certificados no sistema.

Explique na release que o Windows pode mostrar um aviso de aplicativo desconhecido
por falta de assinatura/reputação. Isso não deve ser descrito como garantia de
segurança nem como detecção de vírus. Não oriente a desativar o antivírus ou
a instalar um certificado raiz próprio para executar o programa.

O hash verifica correspondência com a entrega publicada. Para conferir:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath .\certbox-windows-x64.exe
```

Privacidade pública: docs/PRIVACY.pt.md, docs/PRIVACY.en.md e docs/PRIVACY.es.md.
Os avisos de licença próprios e de terceiros já acompanham o executável.

Os arquivos de validação e histórico locais não integram o primeiro envio.
Testes automatizados não substituem a conferência visual manual.
