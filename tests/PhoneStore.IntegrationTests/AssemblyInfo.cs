// N2LHTBLTT-24: Các integration test dùng chung biến môi trường
// (ConnectionStrings__DefaultConnection) để trỏ host về database test riêng,
// nên phải chạy tuần tự để tránh race condition.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
