package main

/*
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int string_length(const char* s) {
    return strlen(s);
}

const char* greet(const char* name) {
    static char buf[256];
    snprintf(buf, sizeof(buf), "Hello, %s!", name);
    return buf;
}
*/
import "C"
import (
	"fmt"
	"unsafe"
)

func main() {
	// Test C.CString and C.GoString
	cs := C.CString("world")
	defer C.free(unsafe.Pointer(cs))

	length := C.string_length(cs)
	fmt.Println("length of 'world':", length)

	greeting := C.greet(cs)
	goGreeting := C.GoString(greeting)
	fmt.Println(goGreeting)

	fmt.Println("CGo string test passed!")
}
