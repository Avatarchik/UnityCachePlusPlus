# UnityCachePlusPlus
I wanted to share a creation of mine, I created a new Unity Cache Server written to speed up caching for large projects. I've noticed in my own projects where we cache 15-25k objects that the stock cache server starts acting up and can't handle lots of local developers or our build lab hitting it. I wrote a better version and I'm looking for people who are willing to help test it out and tell me what they think.

# Benefits of UnityCache++
1. Better Throughput - Random timeouts by the stock server can cause you to rebuild your entire project costing your HOURS of needless build time. UnityCache++ is designed from the ground up to avoid this costly problem by designing for asset throughput.

2. Consistent Response Times - The application is multi-threaded to achieve lightning fast response times even with hundreds of clients. A small blip in the cache server response time can cause Unity to rebuild all the assets in the project.

3. Better Cache Management - Unity deletes file the oldest files first which could result in costly asset reprocessing, UnityCache++ deletes least used files which helps ensure minimal reprocessing.

# Project Roadmap
1. Upstream Servers - Allow the cache server to query other cache servers for assets
2. Peer Caching - Query other computers on the LAN if they have an asset
3. Secure Distributed Team Caching - Use Amazon S3 Buckets to securely cache items for teams who work in remote offices
4. Dual Support for Unity 4 & 5 - Support both versions of Unity in the same cache server.
5. Windows, Mac & Linux Support - Run on your favorite operating system
