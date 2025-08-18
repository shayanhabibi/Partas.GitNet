module SepochSemver.Tests

open Expecto
open Partas.Tools.SepochSemver

[<Tests>]
let SepochSemver =
    testList "SepochSemver" [
        testTheory "Simple Sepochs" [
            "_WILD_"
            "_(Scoped)_"
        ] <| fun input ->
            let sepoch = parseSepoch input
            Expect.equal
                $"{sepoch}"
                input
                $"Can convert {input} into and backfrom Sepoch"
        testTheory "Simple SepochSemvers" [
            "0.1.2"
            "1.5.3"
            "_WILD_0.4.5"
            "_(Scoped)_0.3.1"
            "_RAGE(Core)_3.2.1"
            "_RAGE(Experimental)_3.2.1+432ld-32"
            "_(Pdf)_3.2.1-preview.1"
            "_(Pdf)_0.0.2-preview5"
            "_EPOCH_4.3.6-alpha.8.3+542iop00"
        ] <| fun input ->
            let sepochSemver = parseSepochSemver input
            Expect.equal
                $"{sepochSemver}"
                input
                $"Can convert {input} into and backfrom SepochSemver"
        testTheory "Bump epoch" [
            "0.1.2"
            "_WOKE_0.3.5"
            "0.1.4-alpha.8"
            // "0.4.6+4324" // we leave metadata to the consumer
        ] <| fun input ->
            let actual =
                "ALPHA"
                |> SepochSemver.bumpEpoch (parseSepochSemver input) 
            let expected = {
                Sepoch = Sepoch.Epoch "ALPHA"
                SemVer = Semver.SemVersion(1)
            }
            Expect.equal
                actual
                expected
                $"%A{actual}"
        testTheory "Bump major" [
            "1.5.3"
            "1.2.1024"
            "1.9.0-alpha.3"
        ] <| fun input ->
            let actual =
                parseSepochSemver input
                |> SepochSemver.bumpMajor
            let expected = {
                Sepoch = Sepoch.None
                SemVer = Semver.SemVersion(2)
            }
            Expect.equal
                actual
                expected
                $"%A{actual}"
        testTheory "Bump minor" [
            "6.2.3"
            "6.2.1024"
            "6.2.0-alpha.3"
        ] <| fun input ->
            let actual =
                parseSepochSemver input
                |> SepochSemver.bumpMinor
            let expected = {
                Sepoch = Sepoch.None
                SemVer = Semver.SemVersion(6,3)
            }
            Expect.equal
                actual
                expected
                $"%A{actual}"

        testTheory "Bump minor" [
            "1.0.0"
            "1.0.0-alpha.3"
        ] <| fun input ->
            let actual =
                parseSepochSemver input
                |> SepochSemver.bumpPatch
            let expected = {
                Sepoch = Sepoch.None
                SemVer = Semver.SemVersion(1,0,1)
            }
            Expect.equal
                actual
                expected
                $"%A{actual}"

        
    ]
