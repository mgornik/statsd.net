# statsd.net
A high-performance stats collection service based on [etsy's](http://etsy.com/) [statsd service](https://github.com/etsy/statsd/) and written in c#.net.

# This fork/branch

This copy of the original project has added following features:
* Ability to operate with multiple sources of measurements
* Support for dynamic sources and ranked metrics

This version of the library is primarily aimed at Librato users. It enables publishing of *metric sources* along with the other data in Librato. Standard statsd service does not support sources. To maintain protocol compatibility with statsd, sources are extracted from the metrics names.

# Specifying the metric source

Source of the metric is extracted from the metric name. Statsd client that you use, which publishes metrics to statsd.net service should pack source name inside the metric name. User can specify how these two are incorporated in one string. statsdnet.config file contains new section called *<extensions>* which defines this.

Setting *<sourceAndName>* has an attribute *regex* that defines regular expression which extracts two named capturing groups: *source* and *name*. First one captures source name of the metric and the second one captures actual name.

Default setting matches the behavior of modified statsd.net C# client library (https://github.com/mgornik/statsd.net), which separates source and metric name using pipe character (ASCII 0x7c).

# Dynamic sources / ranked metrics

Single metric can be gathered from multiple sources. This is useful when you want to track same metric on several machines. If you want to track some metrics across different user sessions, it becomes another story since their number is not constrained. Many simultaneous user sessions might generate explosion of sources count. If you just want to track extremes, e.g. "which user runs the most queries against public API", then you can use dynamic sources feature of this version of the statsd.net library.

Dynamic sources are divided into groups. Single metric with its unique name can be bound to group of dynamic sources. For instance metric "Queries.PerUser" can track how many queries has each user run in the last minute. Dynamic sources bound to this metric can be sources with names ranging from 1 to 1000, which matches User ID in your system. If you only care about who is running the most queries, you can mark this metric as ranked, and only save top 3 results.

Specifying all sources that are considered dynamic is not practical, since there might be many of them. Instead, name of the metric will indicate if it is gathered on dynamic sources. 

Configuration file will indicate how to identify dynamic source and what to do with the measurement. Typically, statsd.net will first filter out top X values and will push only them to the back-end. Rest of the measurements will just get cleared out. 

Configuration file statsdnet.config and section *<extensions>* is used to specify how to use dynamic sources. Tag *<dynamicSources>* can contain number of *<source>* subtags, each specifying one group of dynamic sources. Attributes are:

* nameRegex - regular expression to bind metric name with its group of dynamic sources
* keep - how many measurements to keep (and pass to the back-end)
* ranking - top/bottom - should service keep highest values or lowest values

This service is meant to be used with statsd.net C# client library (https://github.com/mgornik/statsd.net). That client library allows for publishing metrics with defined source.

## Key Features
* Enables multiple latency buckets for the same metric to measure things like p90/5min and p90/1hour in one go
* Can receive stats over UDP, TCP and HTTP.
* Compatible with etsy's statds protocol for sending counts, timings, latencies and sets
* Supports [librato.com](http://metrics.librato.com/) as a backend
* Supports writing out to another statsd.net instance for relay configurations

## Download statsd.net
The latest version is v1.4.1.0 (23-Apr-14), and can be downloaded on the [releases page](https://github.com/lukevenediger/statsd.net/releases).

## Project Status
Statsd.net is actively being used in a high-volume multi-site production environment.

## Installation, Guidance, Configuration and Reference Information
* Find all this and more on the **[statsd.net wiki](https://github.com/lukevenediger/statsd.net/tree/master/statsd.net)**

## Coming Soon
* [App Fabric](http://msdn.com/appfabric) and [memcached](http://memcached.org/) support to allow horizontal scaling, with load balancing and storage
* More backends
* Web-based management console with a RESTful API
* Histogram stats
* Calendargram stats - easily count unique values according to calendar buckets

## About the Codebase

### Maintainers
* Luke Venediger - lukev@lukev.net, http://lukevenediger.me/

### Contributors
Thanks to these guys for adding features to statsd.net!

* Josh Clark - https://github.com/joshclark
* Werner van Deventer - http://brutaldev.com
* Eric J. Smith - http://www.codesmithtools.com/

### Licence
MIT Licence.
