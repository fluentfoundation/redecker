---
layout: home

hero:
  name: Redecker
  text: Restore succeeding proves almost nothing
  tagline: Six ways a .NET dependency upgrade breaks without your package manager noticing — and a tool that reads packages instead of guessing from version numbers.
  image:
    src: /redecker-icon-512.png
    alt: Redecker
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: The problems
      link: /problems
    - theme: alt
      text: GitHub
      link: https://github.com/fluentfoundation/redecker

features:
  - title: Upgrades that build everywhere but one place
    details: >
      A package ships MSBuild logic pointing at files it no longer contains. Restore, resolution
      and most target frameworks are fine; one framework dies at build time, often on one OS.
    link: /rules/rdk0001
    linkText: RDK0001

  - title: Platforms that quietly disappear
    details: >
      An upgrade stops shipping a lib/ framework or a runtime identifier. Compilation binds a
      different asset without comment, or it fails on the device rather than in CI.
    link: /rules/rdk0002
    linkText: RDK0002

  - title: Families split by your own updater
    details: >
      Packages that must carry one version get bumped individually, because the updater sees
      packages rather than families. It surfaces at run time as a missing type.
    link: /rules/rdk0003
    linkText: RDK0003

  - title: Packages dragged past their runtime
    details: >
      A 9.0 extension in a net8.0 app works — it just lifts assets out of the shared framework and
      ships them app-local and unoptimised. Nothing ever tells you.
    link: /concepts/framework-bands
    linkText: Framework bands

  - title: Pins nobody dares remove
    details: >
      The comment explaining why a package is held back is not machine-readable, so the pin
      outlives its cause and becomes folklore. Record the exit condition and it retires itself.
    link: /concepts/pin-hints
    linkText: Pin hints

  - title: Advisories with no clean upgrade
    details: >
      The fix needs a major of a different package, or the advisory lists no patched version at
      all. The honest answer is a documented hold with a condition attached, not silence.
    link: /concepts/pin-hints
    linkText: Pin hints
---
