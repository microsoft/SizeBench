#[allow(dead_code)]
#[repr(C)]
pub enum TestRustEnumWithMethod {
    EnumEntry1,
    EnumEntry2,
}

impl TestRustEnumWithMethod {
    #[inline(never)]
    fn call(&self) -> i32 {
        println!("hello world!");
        println!("second line of output");
        5
    }
}

#[inline(never)]
fn free_function() -> i32 {
    println!("free_function");
    21
}

#[no_mangle]
pub extern "C" fn exported_function_to_root_code(val: TestRustEnumWithMethod) -> i32 {
    val.call() + free_function()
}
