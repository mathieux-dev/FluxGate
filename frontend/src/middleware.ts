import { NextRequest, NextResponse } from 'next/server';

export function middleware(request: NextRequest) {
  const token = request.cookies.get('refresh_token');
  const isAuthPage = request.nextUrl.pathname.startsWith('/login');
  const isPublicPage = request.nextUrl.pathname.startsWith('/pay');
  const isApiRoute = request.nextUrl.pathname.startsWith('/api');

  if (isApiRoute) {
    return NextResponse.next();
  }

  if (!token && !isAuthPage && !isPublicPage) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  if (token && isAuthPage) {
    return NextResponse.redirect(new URL('/dashboard', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
