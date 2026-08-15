# Fluffy Bones — Cute Bone Physics

Repositório de desenvolvimento do plugin **Fluffy Bones**, física de bones para
Unity: movimento secundário de caudas, saias, capas, correntes e cabelo.

> **Status: early WIP.** Só o scaffolding existe. Não há solver, não há lógica —
> as classes são stubs vazios com `// TODO`.

## O que tem aqui

Este repositório é um **projeto Unity de desenvolvimento** com o package
embedado em `Packages/com.uayten.fluffybones/`. Abrir o repositório na Unity já
dá um ambiente pronto pra editar e testar o plugin — o package aparece no
Package Manager em *In Project → Custom*, editável.

O produto que vai pra Asset Store é a pasta do package, não o repositório
inteiro.

## Como abrir

1. Unity Hub → **Add** → **Add project from disk** → selecione a pasta do repo.
2. Versão do editor: **6000.4.7f1** — a mesma do STS-DS, que consome o package
   pelo link local descrito abaixo.
3. Na primeira abertura a Unity gera `Library/`, `ProjectSettings/` restantes e
   os `.meta` — tudo ignorado ou commitado conforme o `.gitignore`.

> **Compatibilidade:** o `package.json` declara `"unity": "2022.3"` como mínimo,
> mas o desenvolvimento acontece em 6000.4. Esse campo é só uma declaração — não
> impede o compilador de aceitar API que só existe em Unity 6. Antes de publicar,
> abrir o package num projeto 2022.3 vazio e confirmar que compila.

### Testar dentro de outro projeto

Sem copiar código, adicione ao `Packages/manifest.json` do projeto consumidor:

```json
"com.uayten.fluffybones": "file:../../FluffyBones/Packages/com.uayten.fluffybones"
```

O caminho é relativo à pasta `Packages` do projeto que consome. A Unity trata
como embedded: você edita aqui, o outro projeto recompila.

### Git LFS e merge de YAML

O `.gitattributes` já manda binários (`.fbx`, `.png`, `.psd`, `.wav`, …) pro LFS
e marca os arquivos YAML da Unity com `merge=unityyamlmerge`. Para o LFS:

```bash
git lfs install
```

Para o merge tool, aponte o git pro `UnityYAMLMerge.exe` da sua instalação
(`Editor/Data/Tools/UnityYAMLMerge.exe`) num `merge.unityyamlmerge` na config
global.

## Estrutura

```
FluffyBones/
├─ .editorconfig                  convenções C# (4 espaços, PascalCase, _campo)
├─ .gitattributes                 LFS + unityyamlmerge
├─ .gitignore                     Library/, Temp/, *.csproj, *.sln, …
├─ README.md                      este arquivo
├─ Assets/                        cenas de teste, personagens de demo
├─ ProjectSettings/               config do projeto de desenvolvimento
└─ Packages/
   ├─ manifest.json               deps do projeto de desenvolvimento
   └─ com.uayten.fluffybones/     ◄── O PRODUTO
      ├─ package.json
      ├─ README.md
      ├─ CHANGELOG.md             Keep a Changelog
      ├─ LICENSE.md               proprietária, todos os direitos reservados
      ├─ Runtime/
      │  ├─ FluffyBones.Runtime.asmdef
      │  ├─ FluffyBody.cs
      │  ├─ FluffyChain.cs
      │  ├─ FluffyCollider.cs
      │  └─ FluffyProfile.cs
      ├─ Editor/
      │  ├─ FluffyBones.Editor.asmdef
      │  └─ FluffyChainEditor.cs
      ├─ Tests/
      │  ├─ Runtime/FluffyBones.Tests.Runtime.asmdef
      │  └─ Editor/FluffyBones.Tests.Editor.asmdef
      ├─ Samples~/                cenas de demonstração (vazio)
      └─ Documentation~/          documentação (vazio)
```

## Publicação

O id `com.uayten.fluffybones` é provisório. O fluxo UPM da Asset Store exige
reivindicar um *publisher namespace* no Publisher Portal, e o campo `name` do
`package.json` tem que bater com o technical name que a Unity atribuir ao
produto — o upload valida isso. Renomear depois é barato: o `name` e o nome da
pasta.

## Licença

Proprietária, todos os direitos reservados. Ver
[LICENSE.md](Packages/com.uayten.fluffybones/LICENSE.md).
