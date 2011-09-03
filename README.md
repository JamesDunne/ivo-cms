IVO-CMS
=======

Author
------
James S. Dunne
bittwiddlers.org

Description
-----------
A very non-invasive ASP.NET 4 content management system backed by a pluggable persistence mechanism, with a SQL Server 2008 backend as the primary implementation.

Purpose
-------
Attempt to design a web content management system that is based on the concepts of immutable versionable objects (aka IVO, pronounced /ee'-vho/).

Features
--------
This design enables per-user branches, versionable history, branch merging, content differencing across history and/or branchs, and a simple atomic publish operation that is as simple as updating a reference.

The system enforces that users of the system work independently of one another. There is no chance of colliding work or any locks to worry about. Data is not mutable so there is no need to lock. New objects are always created and their history is preserved in the system.

There is always the possibility that the same work may be duplicated by two users independently but this situation will cause no ill-effects. It can be resolved by simply merging users' branches together and resolving the differences, if any.



IVO
---
The core schema of the persistence store is heavily based on the internal implementation details of git. The core principles are that there are very few core types (`blob`s, `tree`s, `commit`s, `tag`s), that all of these objects are immutable (can never change after being created), and that these objects are content-addressable via SHA-1 hashes.

The concept of content-addressable objects means that a SHA-1 hash uniquely identifies an object based solely on its contents. For instance, a `blob` that has the contents "Hello world" will *always* hash to the same value and thus that `blob` will always have that same `blobid`, no matter what system it is stored in. This fact is exploited in order to store all historical versions of objects and to separate the identity of objects from the persistence storage system/mechanism, i.e. no auto-generated incrementing identity values that are meaningless. Identity is now based on object contents alone.

This concept applies to `tree`s equally well. Each tree node's SHA-1 `treeid` is constructed by hashing the all data that makes up the `tree`'s child nodes: named `blobid`s or `treeid`s, sorted in alphabetical order.

A `commit` is a structure that records a single historical entry that points to a `treeid` which is the exact state of the entire object tree at that point in time, which includes the user's changes to any objects. A `commit` also contains the name of the committer, the date/time committed, a list of parent `commitid`s (generally 1 parent for normal changes, and 2 parents for recording merges), and an optional commit message indicating, in the committer's own words, what has been modified from the parent `commitid`.

An additional type is the `ref`, not mentioned above, which is mutable and is not content-addressable. It is a simple named pointer that is updated each time a new `commit` is made. Each user has his/her own set of `ref`s that track that user's current branch heads. The HEAD `ref` is a pointer to a branch's `ref` so as to always keep track of the latest `commitid` to work off of.

A `tag` is an immutable, named pointer to a `commitid`. Tags are used to permanently label specific `commit`s, like fixed release points "version 1" or "released on 8/1". The names are arbitrary and are assigned by the user that creates the `tag`. They can never be changed after they are created, hence immutable.

Normally, when a `commit` is made by a user to introduce new changes, the user's HEAD `ref`'s `commitid` is made the parent `commitid` for the new `commit`, indicating a linear history.

When a user completes a merge, a new `commit` is made with two parent `commitids` pointing to the two `commit`s that were merged.

Every time a `commit` is made, the current user's HEAD `ref` is updated to point to the new `commitid`.
