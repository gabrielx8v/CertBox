# Atualizações de dependências

Desenvolvido com IA. As propostas automáticas precisam de revisão e testes.

O arquivo [.github/dependabot.yml](../.github/dependabot.yml) monitora os pacotes
NuGet de src/CertBox/CertBox.csproj e o SDK .NET fixado em global.json. Os demais
projetos atuais usam somente o framework ou uma referência a esse projeto, sem
PackageReference próprio. Se receberem pacotes externos, inclua seus diretórios
na configuração. Os caminhos são relativos à raiz pública do repositório,
que corresponde à pasta source no ambiente original.

A busca por versões novas é semanal, às segundas-feiras, às 09:00 no horário de
São Paulo. São permitidos até três PRs simultâneos de versões NuGet e um de SDK.
Esse limite e essa agenda não limitam as atualizações de segurança, controladas
separadamente pelo GitHub. Mantenha Dependabot alerts e security updates ativos.

Os pacotes Avalonia são agrupados para revisão conjunta, tanto nas atualizações
de versão quanto nas de segurança. Isso ajuda a manter a família compatível;
não substitui a conferência das versões propostas e dos dois builds.
O Dependabot propõe alterações por pull request. A configuração não publica
executáveis nem concede aprovação automática às mudanças.

## Revisão antes do merge

1. Leia o diff e as notas upstream, especialmente nas mudanças de versão maior.
2. Instale o SDK de global.json se o PR o alterar. O SDK local em work não é
   atualizado automaticamente só porque esse arquivo mudou.
3. Atualize e confira os dois lockfiles. A seleção de plataforma é condicional,
   portanto não presuma que uma atualização automática verificou ambos:

   ```powershell
   dotnet restore src/CertBox/CertBox.csproj -r win-x64 -p:CertBoxPlatform=windows --force-evaluate
   dotnet restore src/CertBox/CertBox.csproj -r linux-x64 -p:CertBoxPlatform=linux --force-evaluate
   ```

4. Confira se Avalonia.Win32, Avalonia.X11 e os pacotes Avalonia compartilhados
   mantêm versões compatíveis. Confira os diffs de packages.windows.lock.json
   e packages.linux.lock.json; inclua as correções no mesmo PR.
5. Siga [BUILDING.md](../BUILDING.md) para compilar as duas plataformas e rodar
   as verificações apropriadas. CodeQL analisa código; os builds e testes
   verificam compatibilidade e comportamento. A versão Linux permanece sem
   teste manual até que isso seja efetivamente realizado e documentado.
6. Atualize DEPENDENCIES.md, DEPENDENCIES.json e os avisos em licenses conforme
   as versões que serão distribuídas. Se alterar o runtime, confira também a
   versão efetiva incluída em cada publicação.
7. Faça o merge após a revisão. Novos executáveis exigem uma nova release com
   versão, código correspondente e hashes, conforme [RELEASE.md](../RELEASE.md).

## Escopo

CodeQL com configuração Default é gerenciado pelo GitHub e não cria um workflow
versionado neste projeto. Por isso não existe uma entrada github-actions sem
manifesto correspondente. Quando houver arquivos em .github/workflows, acrescente
o monitoramento github-actions com directory: "/".

Atualização de versões e submissão ao grafo de dependências são recursos
distintos. O arquivo define o ecossistema nuget mesmo com o projeto em subpasta,
o que permite ao mecanismo de submissão automática .NET reconhecer o ecossistema.
Esse recurso ainda precisa ser ativado em Advanced Security e sua execução deve
ser conferida. Uma execução em um único ambiente pode não representar as duas
seleções condicionais Windows/Linux; não a trate como inventário completo sem
conferir o resultado.

Referências:

- [Configuração do Dependabot](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/configure-version-updates).
- [Opções da configuração](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference).
- [Submissão automática de dependências .NET](https://docs.github.com/en/code-security/reference/supply-chain-security/automatic-dependency-submission).
