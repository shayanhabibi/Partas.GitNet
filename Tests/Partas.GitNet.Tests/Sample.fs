module GitNet.Tests

open System.Collections.Frozen
open Expecto
open Partas.GitNet
open Partas.GitNet.GitCollection
open LibGit2Sharp.FSharp
open System.Linq
open Partas.GitNet.Types.GitNetTag
open EasyBuild.FileSystemProvider

type Files = AbsoluteFileSystem<__SOURCE_DIRECTORY__>

[<Tests>]
let test =
    let path = Files.``Partas.Solid.TestGround``.``.``
    let repo = Repository.load path
    let config = {
        GitNetConfig.initFSharp with
            RepositoryPath = path
            ProjectType =
                { Defaults.FSharp.projectFSharpConfig with
                    AutoScoping =
                        _.Split('.').Last()
                        >> function
                            "ScratchTests" -> None
                            | scope -> Some scope }
                |> Some
                |> ProjectType.FSharp
    }
    let runtime = new GitNetRuntime(config)
    let collection =
        TagCommitCollection.load runtime
    let tagShaPairs = [
            "0.2.0", "985bcb345a0e4f6bfacd9033a95c6ca98aab3a01"
            "0.2.1", "c7179e5fe939c090e932477b54eb6410fca1eff8"
            "0.2.2", "3d90b0bfdf1c2041a4ce5b822a3fb41dd1f875e5"
            "0.2.4", "7dbbea1db52e79211d8bd8704d85711d2f2d8adc"
            "0.2.5", "93338646a84c92662f3ff58b46d482fa651d5038"
            "0.2.6", "c7d81e5706e279a498b704ee5ecd08bad2a0dcac"
            "0.2.8", "66be470b3c6e6b9c73374a89445354958bc956eb"
            "0.2.9", "c1597a87abcd53362b484dae6a64879e26924e47"
            "0.2.11", "efb3ac07f807979a136a4e3d4aea1c854b788011"
            "0.2.15", "1398250677843987d8791fb77b8da19f0fb683fb"
            "0.2.26", "a6c20f2422ab24cf03d1e621602a1b31c78e3cf9"
            "0.2.28", "f1fe945fa8ee8e739560b276402a17e450a45aa8"
            "0.2.30", "32512fd5d113e96fd12a6c299d2137d8666741c0"
            "0.2.32", "442ce04e77e777bbb7f868cefb5b4995a90f4e7e"
            "0.2.35", "81864905ab62f3d4369f0f741d1fafce1111e63e"
            "1.0.0-alpha1", "92f24174c3f362e37d1380fda3c12172c544e1ba"
            "1.0.0-alpha4", "d72c7080b0508f4230b7d33e0a54fa3320ca2119"
            "1.0.0", "3af2c4b9fd871b0a822eb3d1882ea9a3fc51e852"
            "1.1.5", "1ff681afa0d203f37491147c1cae07dad718f238"
            "1.1.6", "957ba32041231c29efe7a7449ac73b6e2a3879ec"
            "1.2.0", "0e87429f7d4955ad6e7b5465317f2048526686fa"
            "1.2.1", "b9b6864d5eed010c473c4854a778b0a64ed13a9c"
        ]
    testList "Partas.Solid Repo Local Tests" [
        test "Untagged Commits is Empty" {
            let actual = collection.UntaggedCommits
            let expected = FrozenSet<CommitSha>.Empty
            Flip.Expect.equal $"{actual}" expected actual
        }
        test "Contains Scopes" {
            collection.Scopes
            |> _.Keys
            |> Seq.sort
            |> fun actual ->
                Expect.sequenceEqual
                    actual
                    (seq {
                        "FablePlugin"
                        "Plugin"
                        "Core"
                        "Solid"
                        "Compiled"
                    }
                     |> Seq.map Scope
                     |> Seq.sort)
                     "Sequences should be equal"
        }
        test "Scope Paths are Correct" {
            collection.Scopes
            |> _.Values
            |> Seq.sort
            |> Flip.Expect.sequenceEqual
                "Sequences should be equal"
                (seq {
                    "Partas.Solid"
                    "Partas.Solid.FablePlugin"
                    "Partas.Solid.Tests.Core"
                    "Partas.Solid.Tests.Plugin"
                    "Partas.Solid.Tests.Plugin/Compiled"
                } |> Seq.sort)
        }
        ptest "Core Tests should only have one commit" {
            collection.CommitScopes.Values
            |> Seq.collect id
            |> Seq.filter ((=) <| Scope "Partas.Solid.Tests.Core")
            |> Flip.Expect.hasLength
                "Only one commit is expected for this scope"
                1
        }
        testTheory "Tag Commits have expected Shas" tagShaPairs <| fun (tag,sha) ->
            query {
                for gitTag in collection.TagCollection.KeyDictionary do
                where (gitTag.Value |> GitNetTag.Git.name = tag)
                select gitTag.Key
                exactlyOne
            }
            |> Flip.Expect.equal sha (TagSha sha)
        test "Tags are arranged from lowest to highest (most recent)" {
            collection.TagCollection.OrderedKeys
            |> Flip.Expect.sequenceContainsOrder
                "Tags should be arranged correctly"
                (tagShaPairs |> Seq.map (snd >> TagSha))
        }
        testTheory "Tag commits include themselves" tagShaPairs <| fun (tag,sha) ->
            collection.TagCommits[TagSha sha]
            |> Flip.Expect.contains "" (CommitSha sha)
        testTheory "Tag commits are included in Commit Collections" (
            // The 0.2.0 tag is fubar'd in the git tree
            tagShaPairs |> Seq.map (function "0.2.0", _ -> "0.2.0", "e49d71260146fa4404a31259707854af89e3f2e8" | value -> value)
            ) <| fun (tag,sha) ->
            collection.CommitCollection.OrderedKeys
            |> Flip.Expect.contains "" (CommitSha sha)
        testList "Slicing" [
            test "Collection should still give 0 unreleased commits" {
                let collection =
                    collection
                    |> TagCommitCollection.getUnreleasedCommits (
                        tagShaPairs
                        |> List.find (fst >> (=) "1.2.1")
                        |> snd
                        |> TagSha
                        )
                Flip.Expect.hasLength "" 0 collection
            }
            test "Tag 1.2.0 -> Tag 1.2.1 should have 12 commits" {
                
                let tagSha1,tagSha2 =
                    tagShaPairs
                    |> List.find (fst >> (=) "1.2.0")
                    |> (snd >> TagSha),
                    tagShaPairs
                    |> List.find (fst >> (=) "1.2.1")
                    |> (snd >> TagSha)
                    
                collection
                |> TagCommitCollection.getCommitsBetween tagSha1 tagSha2
                |> Seq.sort
                |> Flip.Expect.sequenceEqual "" (seq {
                    "3d11842ae5a11ae290ab351afe10894ee5839879"
                    "dddca72c4d63c33aa30ee9e41e3aad8b4037b17e"
                    "c1fd968f69524183bd4919a445c9a312b5d9177e"
                    "aa2c93223b37c1f872f31b8a68de8bb8db1a0c3d"
                    "20d521a4d64d753ee8a7fb48eb92e8c7bf9b52b9"
                    "e91beb460612c1dfae48cf0de9959be15f83097a"
                    "a57db61940f5461c176d613cb154866902fd3240"
                    "ac435a0392f5a087890acbe263ea347e24263cf9"
                    "8f5d421cce431712130e54063cee3b2afe1e6856"
                    "bc27828882da5cb3c963790ac39f6c458b759583"
                    "41c4449ba1720c1cfad83667a8b312e645311161"
                    "b9b6864d5eed010c473c4854a778b0a64ed13a9c"
                } |> Seq.map CommitSha |> Seq.sort)
            }
            test "Tag 0.2.1 -> 0.2.5 should contain 6 commits" {
                let tag021 =
                    tagShaPairs
                    |> List.find (fst >> (=) "0.2.1")
                    |> snd
                    |> TagSha
                let tag025 =
                    tagShaPairs
                    |> List.find (fst >> (=) "0.2.5")
                    |> snd
                    |> TagSha
                collection
                |> TagCommitCollection.getCommitsBetween tag021 tag025
                |> Seq.sort
                |> Flip.Expect.sequenceEqual "" (seq {
                    "9dd084284e8651e48015901319ffc277a6426647"
                    "3d90b0bfdf1c2041a4ce5b822a3fb41dd1f875e5"
                    "4a88d7fa1a0cc42d451255824159de906f60aaa5"
                    "7dbbea1db52e79211d8bd8704d85711d2f2d8adc"
                    "feb40aedd469cfd09c6340c68bf41a9c96bae01e"
                    "93338646a84c92662f3ff58b46d482fa651d5038"
                } |> Seq.map CommitSha |> Seq.sort)
            }
            test "1.2.0 contains correct scopes" {
                collection.CommitScopes[
                    tagShaPairs
                    |> List.find (fst >> (=) "1.2.0")
                    |> snd |> CommitSha
                ]
                |> Seq.sort
                |> Flip.Expect.sequenceEqual
                    "Sequences should be equal"
                    (seq {
                        "Solid"
                        "FablePlugin"
                        "Plugin"
                        "Compiled"
                    } |> Seq.map Scope |> Seq.sort)
            }
            test "TreeChanges for commits are correct" {
                collection.TreeChangeCollection[
                    tagShaPairs
                    |> List.find (fst >> (=) "1.2.0")
                    |> snd |> CommitSha
                ]
                |> Seq.sort
                |> Flip.Expect.hasLength "" 13
            }
        ]
        
    ]
