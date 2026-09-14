// THE TESTS SHARE ONE DATABASE AND ERASE IT BETWEEN TESTS, SO THEY NEVER RUN IN PARALLEL
[assembly: CollectionBehavior(DisableTestParallelization = true)]
